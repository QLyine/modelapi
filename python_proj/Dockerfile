FROM python:3.8-slim

RUN apt-get update -y && apt-get install -y build-essential

# We copy just the requirements.txt first to leverage Docker cache
COPY ./requirements.txt /app/requirements.txt
COPY ./docker_entrypoint.sh /docker_entrypoint.sh

WORKDIR /app

RUN pip install -r requirements.txt

COPY . /app

ENTRYPOINT [ "bash" ]

#CMD [ "http_server.py" ]
CMD [ "/docker_entrypoint.sh" ]

