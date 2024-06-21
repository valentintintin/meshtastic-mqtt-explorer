window.initializeLeafletMap = (center, zoom) => {
    window.leafletMap = L.map('map', {
        preferCanvas: true
    }).setView(center, zoom);

    window.leafletMarkers = [];

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
    }).addTo(window.leafletMap);

    console.debug('Map created');
};

window.addMarkersToMap = (markers) => {
    markers.forEach(marker => {
        window.addMarkerToMap(marker);
    });
};

window.addMarkerToMap = (marker) => {
    let pin = window.leafletMarkers[marker.id];

    if (marker.iconType === 0) {
        const icon = new L.Icon({
            iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-' + (marker.color ?? 'blue') + '.png',
            shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-shadow.png',
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
    }
    else if (marker.iconType === 1) {
        if (pin) {
            pin.setLatLng([marker.latitude, marker.longitude]);
            pin.setStyle({
                color: marker.color ?? 'blue',
                fillColor: marker.fillColor ?? '*',
            });

            if (marker.popup) {
                pin.getPopup().setContent(marker.popup);
            }

            return;
        }

        pin = L.circleMarker([marker.latitude, marker.longitude], {
            radius: 5,
            fillOpacity: marker.opacity ?? 0.2,
            color: marker.color ?? 'blue',
            fillColor: marker.fillColor ?? '*',
            weight: 2,
        });
    }

    const label = marker.popup ?? marker.label;
    
    if (label) {
        pin = pin.bindPopup(label, {
            keepInView: true,
            closeButton: true,
            autoClose: true,
            closeOnEscapeKey: true
        }).on({
            click(e) {
                window.leafletMap.panTo(e.latlng);
            },
            mouseover(e) {
                // this.openPopup();
            }
        });
    }

    pin.addTo(window.leafletMap);
    
    window.leafletMarkers[marker.id] = pin;
};

window.addPolylinesToMap = (lines) => {
    lines.forEach(line => {
        window.addPolylineToMap(line);
    });
};

window.addPolylineToMap = (line) => {
    let polyline = L.polyline(line.points, {
        color: line.color,
        weight: 1
    }).addTo(window.leafletMap);

    if (line.popup) {
        polyline = polyline.bindPopup(line.popup, {
            keepInView: true,
            closeButton: true,
            autoClose: true,
            closeOnEscapeKey: true
        });
    }
    
    window.leafletMarkers[line.id] = polyline;
};

window.clearMarkersMap = () => {
    for (let leafletMarkersKey in window.leafletMarkers) {
        const leafletMarkers = window.leafletMarkers[leafletMarkersKey];
        leafletMarkers.remove();
    }

    window.leafletMarkers.length = 0;
};

window.disposeLeafletMap = () => {
    if (window.leafletMap && window.leafletMap.remove) {
        window.leafletMap.off();
        window.leafletMap.remove();
        window.leafletMarkers.length = 0;

        console.debug('Map destroyed');
    }
}