#!/bin/bash

server="valentin@valentin.ddns.info"
port=22

set -e
set -x

rm -Rf ../published-Recorder/*
dotnet publish --no-restore --no-self-contained --nologo --output ../published-Recorder/

rsync -e "ssh -p $port" -r --info=progress2 ../published-Recorder/ $server:/home/valentin/docker/meshtastic_mqtt_explorer/Recorder/

ssh $server -t -p $port "docker restart meshtastic-meshtastic_mqtt_explorer_recorder-1 && docker logs -f --tail 10 meshtastic-meshtastic_mqtt_explorer_recorder-1"