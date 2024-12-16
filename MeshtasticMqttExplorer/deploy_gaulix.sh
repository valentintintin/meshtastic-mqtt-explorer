#!/bin/bash

server="flowapps.link"
port=2234

set -e
set -x

rm -Rf ../published-Front/*
dotnet publish --no-restore --no-self-contained --nologo --output ../published-Front/

rsync -e "ssh -p $port" -r --info=progress2 ../published-Front/ $server:/mnt/containers/meshtastic-mqtt-explorer/Front/

ssh $server -t -p $port "docker restart meshtastic-mqtt-explorer-meshtastic_mqtt_explorer-1 && docker logs -f --tail 10 meshtastic-mqtt-explorer-meshtastic_mqtt_explorer-1"
