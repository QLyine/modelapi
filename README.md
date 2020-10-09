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

#### High Level Architect & Design

  * Etcd is a distributed database, usually used as a coordinator between process,
  also as a service discovery.
  Etcd will also send back to listeners ( .NET Broker & Model Web API ) changes
  that took place in it's data.


  * Scylla is a NoSQL, chosen for simplicty, and is used as a datastore for the
  models. A better datastore ( for bigger models ) is an Object Store + Database
  ( Storing Metadata )

  * .NET broker is used almost as a proxy and as a master that coordinates the
  model api

  * Python Model API which contains a REST API and the Model


![alt text](https://raw.githubusercontent.com/QLyine/modelapi/master/Diagram.png)

#### Low Level ( Detailed ) Architect & Design

##### Fit Model ( Operation )

When the .NET Broker receives that operation it does the following steps:

  * Get current model version 
  * Increment model version
  * Publish on etcd the incremented model version ( which through etcd will
  signal the Model Web APIs to reload the model with the model version published )
  * Removes of it's memory the Model Web API ( Python ) hosts and ports that
  have different model version
  * Sends the data to be fit to an Model Web API ( if failure happens try a
  different Model Web API )
  
When the .NET Broker receives the notification from etcd that directory
"nodes" changed ( which is where each Model Web APIs publish their location IP +
PORT and their current loaded model version ):

  * For every notification received it matches the model version with the one
  that is designated to be served and if it matches it stores in memory a list
  of APIs that can be called to do the Forecast

When the Model Web API receives that operation it does the following steps:

  * Run fit model on the new arrived data 
  * Store the binary model on the database along with it's model version
  So other Model Web API can reload the model 
  
When the Model Web API received the notification from etcd that model version
changed:

  * It will fetch from the database ( Scylla ) the model corresponding to the model version
  that it received from the notification 
  * If the model is not yet present on the database then it sleeps and retries
  * Once loaded into memory from the database it publishes on etcd on "nodes"
  directory it's IP + PORT + Model Version currently loaded


##### Forecast Model ( Operation )

  * .NET broker will simply forward the operation to the Model Web API
  * If no node available ( either because there is none or because they are
  reloading their model ) it awaits and retries in a backoff manner, until a
  MAX certain period of time.

#### Pit Falls + Improvements

#### Fit Model

Problem: If a node or every Model Web API node fails to fit model, then for every
subsequent fit model failure will happen, because at that point forward there 
.NET has lost the location of the Model Web API.

Solution: Either revert to initial state ( ModelVersion - 0 ) and thus all nodes
are going to be discoverable. Or revert to the latest model on the database.
Or .NET Broker will always maintain all Model Web API for the fit model, and
keep another list for forecast endpoint.

Preferred Solution: Have a dedicated process to the fit model process, connected
to Kafka or other message broker, in order to guarantee that at least process
does fit the model.


