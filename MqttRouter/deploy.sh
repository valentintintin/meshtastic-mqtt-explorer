#!/bin/bash

server="valentin.ddns.info"
port=22

set -e
set -x

rm -Rf ../published-Router/*
dotnet publish --no-restore --no-self-contained --nologo --output ../published-Router/

rsync -e "ssh -p $port" -r --info=progress2 ../published-Router/ $server:/home/valentin/docker/meshtastic_mqtt_explorer/MqttRouter

ssh $server -t -p $port "docker restart meshtastic-meshtastic_mqtt_explorer_router-1 && docker logs -f --tail 10 meshtastic-meshtastic_mqtt_explorer_router-1"