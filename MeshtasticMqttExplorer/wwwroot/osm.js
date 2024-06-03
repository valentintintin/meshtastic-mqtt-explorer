window.initializeLeafletMap = (center, zoom) => {
    window.leafletMap = L.map('map').setView(center, zoom);
    window.leafletMarkers = [];

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    }).addTo(window.leafletMap);
};

window.addMarkersToMap = (markers) => {
    markers.forEach(marker => {
        window.addMarkerToMap(marker);
    });
};

window.addMarkerToMap = (marker) => {
    let pin = window.leafletMarkers[marker.id];

    if (pin) {
        pin.setLatLng([marker.latitude, marker.longitude]);
        pin.setIcon(marker.svg ? new L.divIcon({
            html: marker.svg,
            className: "",
            iconSize: [40, 40],
            iconAnchor: [10, 0],
        }) : new L.Icon({
            iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-' + (marker.color ?? 'blue') + '.png',
            shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-shadow.png',
            iconSize: [25, 41],
            iconAnchor: [12, 41],
            popupAnchor: [1, -34],
            shadowSize: [41, 41]
        }));

        if (marker.popup) {
            pin.getPopup().setContent(marker.popup);
        }
        
        return;
    }
    
    pin = L.marker([marker.latitude, marker.longitude], {
        title: marker.label ?? '',
        icon: marker.svg ? new L.divIcon({
            html: marker.svg,
            className: "",
            iconSize: [40, 40],
            iconAnchor: [10, 0],
        }) : new L.Icon({
            iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-' + (marker.color ?? 'blue') + '.png',
            shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-shadow.png',
            iconSize: [25, 41],
            iconAnchor: [12, 41],
            popupAnchor: [1, -34],
            shadowSize: [41, 41]
        })
    }).addTo(window.leafletMap);
    
    if (marker.popup) {
        pin = pin.bindPopup(marker.popup, {
            keepInView: true,
            closeButton: true,
            autoClose: false,
            closeOnEscapeKey: true
        })
    }

    window.leafletMarkers[marker.id] = pin;
};

window.addPolylineToMap = (points, color) => {
    L.polyline(points, { color })
        .addTo(window.leafletMap);
};

window.disposeLeafletMap = () => {
    if (window.leafletMap && window.leafletMap.remove) {
        window.leafletMap.off();
        window.leafletMap.remove();
        window.leafletMarkers.length = 0;
    }
}