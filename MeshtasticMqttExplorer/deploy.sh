#!/bin/bash

server="valentin@valentin.ddns.info"
port=22

set -e
set -x

rm -Rf ../published-Front/*
dotnet publish --no-restore --no-self-contained --nologo --output ../published-Front/

rsync -e "ssh -p $port" -r --info=progress2 ../published-Front/ $server:/home/valentin/docker/meshtastic_mqtt_explorer/Front/

ssh $server -t -p $port "docker restart meshtastic-meshtastic_mqtt_explorer-1 && docker logs -f --tail 10 meshtastic-meshtastic_mqtt_explorer-1"