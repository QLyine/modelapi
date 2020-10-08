import numpy as np
import pickle 
from datetime import datetime
from statsmodels.tsa.arima_model import ARIMA
from cassandra.cluster import Cluster

import pandas as pd

def difference(dataset, interval=365):
    diff = list()
    for i in range(interval, len(dataset)):
        value = dataset[i] - dataset[i - interval]
        diff.append(value)

    return np.array(diff)

def inverse_difference(history, yhat, interval=365):
    return yhat + history[-interval]

def fit_model(X):
    differenced = difference(X)
    model = ARIMA(differenced, order=(7,0,1))
    model_fit = model.fit(disp=0)

    return model_fit

def forecast(X, model, num_steps):
    forecast = model.forecast(num_steps)[0]
    forecast = inverse_difference(X, forecast)

    return forecast



series = pd.read_csv('dataset.csv', header=0, index_col=0)
X = series.values
num_steps = 5000

model = fit_model(X)
print("Model Fil ", model)

bytesmodel = pickle.dumps(model)
modelLoaded = pickle.loads(bytesmodel)


#print(bytesmodel)
forecast_result = forecast(X, modelLoaded, num_steps)

print(forecast_result)

scylla = Cluster(['127.0.0.1'])
session = scylla.connect("test")
insert_ql = "INSERT INTO test.models (version, time, model) VALUES (?, ?, ?)"
insert = session.prepare(insert_ql)
session.execute(insert, [10, datetime.utcnow(), bytesmodel])


