#!/bin/bash

IF="${MODEL_IF}"
if [[ -z "$IF" ]] ; then
	IF="eth0"
fi

PORT="${MODEL_PORT}"
if [[ -z "$PORT" ]] ; then
	PORT="8090"
fi

SCYLLA="${MODEL_SCYLLA}"
if [[ -z "$SCYLLA" ]] ; then
	SCYLLA="127.0.0.1"
fi

ETCD_HOST="${MODEL_ETCD_HOST}"
if [[ -z "$ETCD_HOST" ]] ; then
	ETCD_HOST="127.0.0.1"
fi

python /app/http_server.py -b $IF -s ${SCYLLA} -e ${ETCD_HOST}
