window.initializeLeafletMap = (center, zoom) => {
    
    window.leafletMap = L.map('map', {
        preferCanvas: true
    }).setView(center, zoom);

    window.leafletMarkers = [];

    let layer = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
    })

    // layer = protomapsL.leafletLayer({url: 'mymap.pmtiles', theme: "light"});
    
    layer.addTo(window.leafletMap);

    console.debug('Map created');
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
            icon: icon
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
        weight: 2
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