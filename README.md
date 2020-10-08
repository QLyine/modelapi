# README

### Pre-Requistes on the machine that will run the project

  * Have docker installed
  * Have docker-compose installed
  * Run build_broker.sh
  * Run build_python_model_api.sh
  * Run docker pull scylladb/scylla
  * Run docker pull bitnami/etcd

### Run work environment

  * docker-compose up

#### Examples

  * To fit the model

  curl -H 'Content-type: application/json' -XPOST -i http://127.0.0.1:5000/api/fit_model -d@dataset_simple

  * To forecast

  curl -H 'Content-type: application/json' -XGET -i http://127.0.0.1:5000/api/forecast/500


### Design & Architect

#### High Level Architect

  * Etcd is a distributed database, usually as a coordinator between process,
  also as a service discovery.

  * Scylla is a NoSQL, chosen for simplicty, and is used as a datastore for the
  models. A better datastore ( for bigger models ) is an Object Store + Database
  ( Storing Metadata )

  * .NET broker is used almost as a proxy and as a master that coordinates the
  model api

  * Python Model API which contains a REST API and the Model

![alt text](https://raw.githubusercontent.com/QLyine/modelapi/master/Diagram.png)





