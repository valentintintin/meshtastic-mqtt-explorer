#!/bin/bash

server="valentin@valentin.ddns.info"
port=22

set -e
set -x

rm -Rf ../published-Worker/*
dotnet publish --no-restore --no-self-contained --nologo --output ../published-Worker/

rsync -e "ssh -p $port" -r --info=progress2 ../published-Worker/ $server:/home/valentin/docker/meshtastic_mqtt_explorer/Worker/

ssh $server -t -p $port "docker restart meshtastic-meshtastic_mqtt_explorer_worker-1 && docker logs -f --tail 10 meshtastic-meshtastic_mqtt_explorer_worker-1"