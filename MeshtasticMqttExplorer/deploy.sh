#!/bin/bash

server="valentin.ddns.info"
port=22

set -e
set -x

rm -Rf ../published/*
dotnet publish --no-restore --no-self-contained --nologo --output ../published/

rsync -e "ssh -p $port" -r --info=progress2 ../published/ $server:/home/valentin/docker/meshtastic_mqtt_explorer/Front/

ssh $server -t -p $port "cd /home/valentin/docker/meshtastic_mqtt_explorer/ && docker restart meshtastic-meshtastic_mqtt_explorer-1 && docker logs -f --tail 10 meshtastic-meshtastic_mqtt_explorer-1"