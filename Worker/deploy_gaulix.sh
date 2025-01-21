#!/bin/bash

server="flowapps.link"
port=2234

set -e
set -x

rm -Rf ../published-Worker/*
dotnet publish --no-restore --no-self-contained --nologo --output ../published-Worker/

rsync -e "ssh -p $port" -r --info=progress2 ../published-Worker/ $server:/mnt/containers/meshtastic-mqtt-explorer/Worker/

ssh $server -t -p $port "docker restart meshtastic-mqtt-explorer-meshtastic_mqtt_worker-1 && docker logs -f --tail 10 meshtastic-mqtt-explorer-meshtastic_mqtt_worker-1"
