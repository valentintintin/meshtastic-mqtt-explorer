window.initializeLeafletMap = (where) => {
    window.leafletMarkers = [];

    const osm = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    });
    
    const topo = L.tileLayer('https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png', {	
        maxZoom: 17,
        attribution: 'Map data: &copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors, <a href="http://viewfinderpanoramas.org">SRTM</a> | Map style: &copy; <a href="https://opentopomap.org">OpenTopoMap</a> (<a href="https://creativecommons.org/licenses/by-sa/3.0/">CC-BY-SA</a>)'
    });
    
    const satellite = L.geoportalLayer.WMTS({
        layer: "ORTHOIMAGERY.ORTHOPHOTOS",
    }, {
        opacity: 0.4
    });
    
    const courbeNiveau = L.geoportalLayer.WMTS({
        layer: "ELEVATION.SLOPES",
    }, {
        opacity: 0.4
    });

    const proto = protomapsL.leafletLayer({
        url: 'mymap.pmtiles',
        theme: "light"
    });
    
    window.leafletMap = L.map('map', {
        preferCanvas: true,
        layers: [osm, topo, satellite, courbeNiveau, proto],
    }).setView({
        lat: where.latitude,
        lng: where.longitude
    }, where.zoom);

    const layerSwitcher = L.geoportalControl.LayerSwitcher({
        layers : [
            {
                layer : osm,
                config : {
                    title : "OpenStreetMap",
                    description : "Couche Open Street Maps"
                }
            },
            {
                layer : topo,
                config : {
                    title : "OpenTopoMap",
                    description : "Couche Open Topo Maps",
                    visibility: false
                }
            },
            {
                layer : satellite,
                config : {
                    title : "IGN Satellite",
                    description : "Couche satellite issue de l'IGN",
                    visibility: false
                }
            },
            {
                layer : courbeNiveau,
                config : {
                    title : "IGN Altitude",
                    description : "Couche coloriée de l'altitude issue de l'IGN"
                }
            },
            {
                layer : proto,
                config : {
                    title : "ProtoMaps AURA",
                    description : "Couche stockée sur le serveur",
                    visibility: false
                }
            }
        ]
    });
    window.leafletMap.addControl(layerSwitcher);
    
    const elevationPath = L.geoportalControl.ElevationPath();
    window.leafletMap.addControl(elevationPath);
    
    const mousePosition = L.geoportalControl.MousePosition({
        displayCoordinate : true,
        editCoordinates: true,
        altitude : {
            triggerDelay : 250
        }
    });
    window.leafletMap.addControl(mousePosition);
    
    console.debug('Map created');
};

window.addMapChangeEventListener = (dotNetObjectRef) => {
    console.log('event initialized');
    window.leafletMap.on('zoomend', (event) => {
        dotNetObjectRef.invokeMethodAsync('OnMapChangeEvent', window.getLatitudeLongitudeZoomFromMap())
    });
    
    window.leafletMap.on('moveend', (event) => {
        dotNetObjectRef.invokeMethodAsync('OnMapChangeEvent', window.getLatitudeLongitudeZoomFromMap())
    });
};

window.setLatitudeLongitudeZoomToMap = (data) => {
    console.debug('Set', data);
    
    window.leafletMap.setView({
        lat: data.latitude,
        lng: data.longitude
    }, data.zoom);
};

window.getLatitudeLongitudeZoomFromMap = () => {
    const center = window.leafletMap.getCenter();
    return {
        latitude: center.lat,
        longitude: center.lng,
        zoom: window.leafletMap.getZoom()
    };
};

window.addMarkersToMap = (markers) => {
    console.debug('Add', markers.length, 'makers')
    
    markers.forEach(marker => {
        window.addMarkerToMap(marker);
    });
};

window.addMarkerToMap = (marker) => {
    let pin = window.leafletMarkers[marker.id];

    if (marker.iconType === 0) {
        const icon = new L.Icon({
            iconUrl: 'images/markers/marker-icon-' + (marker.color ?? 'blue') + '.png',
            shadowUrl: 'images/markers/marker-shadow.png',
            iconSize: [25, 41],
            iconAnchor: [12, 41],
            popupAnchor: [1, -34],
            shadowSize: [41, 41]
        });

        if (pin) {
            pin.setLatLng([marker.latitude, marker.longitude]);
            pin.setIcon(icon);

            if (marker.popup) {
                pin.getPopup().setContent(marker.popup);
            }

            return;
        }

        pin = L.marker([marker.latitude, marker.longitude], {
            title: marker.label ?? '',
            alt: marker.label ?? '',
            icon: icon,
            zIndexOffset: 1000
        });
    } else {
        if (pin) {
            pin.setLatLng([marker.latitude, marker.longitude]);
            pin.setStyle({
                color: marker.color ?? 'blue',
                fillColor: marker.fillColor ?? marker.color ?? '*',
            });
            
            if (marker.popup) {
                pin.getPopup().setContent(marker.popup);
            }

            return;
        }

        if (marker.iconType === 1) {
            pin = L.circleMarker([marker.latitude, marker.longitude], {
                radius: 7,
                fillOpacity: marker.opacity ?? 0.2,
                color: marker.color ?? 'blue',
                fillColor: marker.fillColor ?? marker.color ?? '*',
                weight: 2,
            });
        } else if (marker.iconType === 2) {
            let radiusMts = 10;
            let bounds = L.latLng([marker.latitude, marker.longitude]).toBounds(radiusMts);
            
            pin = L.rectangle(bounds, {
                fillOpacity: marker.opacity ?? 0.2,
                color: marker.color ?? 'blue',
                fillColor: marker.fillColor ?? marker.color ?? '*',
                weight: 2,
            });
        }
    }

    const label = marker.popup ?? marker.label;
    
    if (label) {
        let polylines = null;
        
        if (marker.linesOnHover) {
            polylines = marker.linesOnHover.map(line => window.createPolyline(line));
        }
        
        pin = pin.bindPopup(label, {
            keepInView: true,
            closeButton: true,
            autoClose: true,
            closeOnEscapeKey: true
        }).on({
            click(e) {
                console.log(marker.id);
                if (polylines) {
                    polylines.forEach(l => l.addTo(window.leafletMap));
                }
                window.leafletMap.panTo(e.latlng);
            },
            mouseover(e) {
                if (marker.popupOnHover) {
                    this.openPopup();
                }
                if (polylines) {
                    polylines.forEach(l => l.addTo(window.leafletMap));
                }
            },
            mouseout(e) {
                if (marker.popupOnHover) {
                    setTimeout(() => {
                        this.closePopup();
                    }, 1500);
                }
                
                if (polylines) {
                    setTimeout(() => {
                        polylines.forEach(l => window.leafletMap.removeLayer(l));
                    }, 3000);
                }
            }
        });
    }

    pin.addTo(window.leafletMap);
    
    pin.bringToFront();
    
    window.leafletMarkers[marker.id] = pin;
};

window.addPolylinesToMap = (lines) => {
    console.debug('Add', lines.length, 'polylines')

    lines.forEach(line => {
        window.addPolylineToMap(line);
    });
};

window.createPolyline = (line) => {
    let polyline = L.polyline(line.points, {
        color: line.color,
        weight: 5
    });

    if (line.popup) {
        polyline = polyline.bindPopup(line.popup, {
            keepInView: true,
            closeButton: true,
            autoClose: true,
            closeOnEscapeKey: true
        }).on({
            click(e) {
                console.log(line.id);
                this.openPopup();
            },
            mouseout(e) {
                setTimeout(() => {
                    this.closePopup();
                }, 3000);
            },
        });
    }
    
    return polyline;
};

window.addPolylineToMap = (line) => {
    let polyline = window.leafletMarkers[line.id];
    
    if (polyline) {
        return;
    }

    polyline = window.createPolyline(line).addTo(window.leafletMap);
    
    polyline.bringToBack();
    
    window.leafletMarkers[line.id] = polyline;
};

window.clearMarkersMap = () => {
    for (const [id, leafletMarker] of Object.entries(window.leafletMarkers)) {
        window.leafletMap.removeLayer(leafletMarker);
        delete window.leafletMarkers[id];
    }

    window.leafletMarkers = {};
};

window.disposeLeafletMap = () => {
    if (window.leafletMap) {
        window.clearMarkersMap();        
        window.leafletMap.off();
        window.leafletMap.remove();
        window.leafletMap = null;

        console.debug('Map destroyed');
    }
}