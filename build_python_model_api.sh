#!/bin/bash

ROOT_DIR="$(dirname "$(readlink -f "$0")")"

cd $ROOT_DIR/python_proj

docker build -t "model_api" .
