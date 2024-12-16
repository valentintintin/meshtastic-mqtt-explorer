#!/bin/bash

server="flowapps.link"
port=2234

set -e
set -x

rm -Rf ../published-Router/*
dotnet publish --no-restore --no-self-contained --nologo --output ../published-Router/

rsync -e "ssh -p $port" -r --info=progress2 ../published-Router/ $server:/mnt/containers/meshtastic-mqtt-explorer/MqttRouter/

ssh $server -t -p $port "docker restart meshtastic-mqtt-explorer-meshtastic_mqtt_router-1 && docker logs -f --tail 10 meshtastic-mqtt-explorer-meshtastic_mqtt_router-1"
