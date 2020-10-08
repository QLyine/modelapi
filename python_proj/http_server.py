import argparse
import netifaces as ni
import json
import etcd3
import threading
import pickle
import atomos.atomic
import traceback

import numpy as np
from statsmodels.tsa.arima_model import ARIMA

import pandas as pd
import time

from cassandra.cluster import Cluster
from flask import Flask, jsonify, request
from flask_restful import reqparse, abort, Api, Resource
from datetime import datetime

MODEL_VERSION_KEY = "modelVersion"
NODES_KEY_TEMPLATE = "nodes/{}_{}"

app = Flask(__name__)
api = Api(app)

Model_State = atomos.atomic.AtomicReference()

class ModelVersion:
    def __init__(self, version, timestamp):
        self.version = version
        self.timestamp = timestamp

    def getVersion(self):
        return self.version

    def getTimestamp(self):
        return self.timestamp

def wait_for_port(port, host='localhost', timeout=360.0):
    print("Waiting for " + host + " : " + port)
    start_time = time.perf_counter()
    while True:
        try:
            with socket.create_connection((host, port), timeout=timeout):
                break
        except OSError as ex:
            time.sleep(0.01)
            if time.perf_counter() - start_time >= timeout:
                raise TimeoutError('Waited too long for the port {} on host {} to start accepting '
                                    'connections.'.format(port, host)) from ex

def init_scylla_db(scylla_session):
    scylla_session.execute("CREATE KEYSPACE IF NOT EXISTS test WITH REPLICATION = {'class' : 'SimpleStrategy', 'replication_factor' : 1 };")
    scylla_session.execute("CREATE TABLE IF NOT EXISTS test.models (version int, time timestamp, model blob, PRIMARY KEY (version, time));")

def read_model_from_db(scylla_session, modelVersion):
    byte_array = bytearray()
    row = scylla_session.execute("SELECT * FROM test.models WHERE version = " + str (modelVersion)).one()
    if (row is None):
        print("No model on database")
        print("Sleeping")
    else:
        byte_array.extend(row.model)
    try:
        modelLoaded = pickle.loads(byte_array)
        Model_State.set(modelLoaded)
    except:
        traceback.print_exc()




def getIpFromInterface(bindIf='eth0'):
    ni.ifaddresses(bindIf)
    ip = ni.ifaddresses(bindIf)[ni.AF_INET][0]['addr']
    return ip

def etcd_get_model_version(etcd_client):
    try:
        valueJson = etcd_client.get(MODEL_VERSION_KEY)
        if (valueJson is None or valueJson[0] is None):
            return ModelVersion(0, 0)
        modelVersion = json.loads(valueJson[0])
        return ModelVersion(modelVersion['version'], modelVersion['timestamp'])
    except:
        traceback.print_exc()
        return ModelVersion(0, 0)

def etcd_get_lease(etcd_client, lease_id, ttl):
    lease_id_hash = hash (lease_id)
    print("lease_id_hash - ", lease_id_hash)
    return etcd_client.lease(ttl, lease_id_hash)

def etcd_refresh_lease(lease, interval=5):
    while True:
        time.sleep(interval)
        lease.refresh()

def etcd_create_node(etcd_client, lease, ip, port, modelVersion):
    key = NODES_KEY_TEMPLATE.format(ip, port)
    val_dict = {'host': ip, 'port': port, 'model': modelVersion}
    val_json = json.dumps(val_dict)
    etcd_client.put(key, val_json, lease)



def difference(dataset, interval=365):
    diff = list()
    for i in range(interval, len(dataset)):
        value = dataset[i] - dataset[i - interval]
        diff.append(value)

    return np.array(diff)

def fit_model(X):
    differenced = difference(X)
    model = ARIMA(differenced, order=(7,0,1))
    model_fit = model.fit(disp=0)

    return model_fit


parser = argparse.ArgumentParser(description='ARIMA Web API')
parser.add_argument("-p", "--port", type=int, default=8090, help="Http server port (default: 8090)")
parser.add_argument("-e", "--ectd-host", dest="etcdHost", default="127.0.0.1", help="Etcd Host (default: 127.0.0.1)")
parser.add_argument("-ep", "--ectd-port", type=int, dest="etcdPort", default=2379, help="Etcd Port (default: 2379)")
parser.add_argument("-b", "--bindIf", dest="bindIf", default="eth0", help="bind to ip (default: eth0)")
parser.add_argument("-s", "--scylla", dest="scylla", default="127.0.0.1", help="scylla host (default: 127.0.0.1)")
parser.add_argument("-sp", "--scylla-port", dest="scylla_port", type=int, default=9042, help="scylla port (default: 9042)")


args = parser.parse_args()

bindIp = getIpFromInterface(args.bindIf)
port = args.port
etcd_host = args.etcdHost
etcd_port = args.etcdPort
scylla_host = args.scylla
scylla_port = args.scylla_port

print("Etc - Host {} | Port {}", etcd_host, etcd_port)

wait_for_port(etcd_port, etcd_host)
wait_for_port(scylla_port, scylla_host)

etcd_client = etcd3.client(host=etcd_host, port=etcd_port)

initialModelVersion = etcd_get_model_version(etcd_client)
print("Initial Model Version - ", initialModelVersion.getVersion())
node_id = '{}_{}'.format(bindIp, port)
lease = etcd_get_lease(etcd_client, node_id, 10)

threading.Thread(target=etcd_refresh_lease, args=(lease,)).start()

etcd_create_node(etcd_client, lease, bindIp, port, initialModelVersion.getVersion())

scylla_client = Cluster([scylla_host])

# Init DB
init_scylla_db(scylla_client.connect())

scylla_session = scylla_client.connect('test')

insert_ql = scylla_session.prepare("INSERT INTO test.models (version, time, model) VALUES (?, ?, ?)")

#load

if (initialModelVersion.getVersion() > 0):
    read_model_from_db(scylla_session, initialModelVersion.getVersion())

def watch_callback(event):
    try:
        modelVersionEvent = event.events[0]
        if (modelVersionEvent is not None and modelVersionEvent.value is not None and len(modelVersionEvent.value) > 0 ):
            value_bytes = modelVersionEvent.value
            modelVersionJson = json.loads(value_bytes)
            modelVersion = ModelVersion(modelVersionJson['version'], modelVersionJson['timestamp'])
            stop = False
            byte_array = bytearray()
            while not stop:
                row = scylla_session.execute("SELECT * FROM test.models WHERE version = " + str (modelVersion.getVersion())).one()
                if (row is None):
                    print("No model on database")
                    print("Sleeping")
                    time.sleep(1)
                else:
                    byte_array.extend(row.model)
                    stop = True
            modelLoaded = pickle.loads(byte_array)
            Model_State.set(modelLoaded)
            etcd_create_node(etcd_client, lease, bindIp, port, modelVersion.getVersion())
    except:
        traceback.print_exc()


etcd_client.add_watch_callback(MODEL_VERSION_KEY, watch_callback)


class ModelApiForecast(Resource):
    def get(self, num_steps):
        model = Model_State.get()
        if model is None:
            print("Model is not yet up!")
            return {'error': 'Model is not yet created'}, 500
        else:
            values = model.forecast(num_steps)[0]
            return {'data': list(values)}

class ModelApiFit(Resource):
    def post(self):
        try:
            json_data = request.get_json(force=True)
            modelVersion = json_data['modelVersion']
            data = json_data['data']
            model = fit_model(np.array(data))
            model_bytes = pickle.dumps(model)
            scylla_session.execute(insert_ql, [modelVersion, datetime.utcnow(), model_bytes])
        except Exception as e:
            print(e)
            return "", 500
        else:
            return "", 200




api.add_resource(ModelApiForecast, '/api/forecast/<int:num_steps>')
api.add_resource(ModelApiFit, '/api/fit_model')

if __name__ == '__main__':
    app.run(port=port, host=bindIp, debug=True)
