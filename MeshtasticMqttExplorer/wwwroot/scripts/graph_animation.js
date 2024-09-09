function log(message) {
 const timestamp = new Date().toISOString().split("T")[1].slice(0, 12);
 console.log(`${timestamp} - ${message}`);
}

function getNodeHexId(id) {
 return id.toString(16).slice(-4).toUpperCase();
}

function calculateBackoffDelay(role) {
 if(role === "IP_GATEWAY")
  return 0;
 else if(role === "Router")
  return Math.random() * 500 + 500;
 else
  return Math.random() * 1500 + 1000;
}

const animationManager = {
 transmissionTimeouts: [],
 nodeStates: {},
 linkStates: {},
 initialNode: null,
 restartTimeout: null,
 maxRetries: 5,

 initialize(mesh) {
  this.nodeStates = {};
  this.linkStates = {};
  this.initialNode = null;

  mesh.nodes.forEach(node => {
   this.nodeStates[node.id] = {
    isTransmitting: false,
    hasTransmitted: false,
    delay: 0,
    role: node.role,
    txCount: 0,
    rxCount: 0,
    shortName: node.short_name,
    logName: `[${node.short_name} ${getNodeHexId(node.id)}]`,
    retryCount: 0,
    currentTimeout: null
   };
  });

  mesh.links.forEach(link => {
   const linkKey = this.getLinkKey(link.source, link.target);
   this.linkStates[linkKey] = {
    isBusy: false,
    lastTransmissionTime: 0,
    receptionConfirmed: false
   };
  });

  log("State initialized for all nodes and links");
 },

 getLinkKey(sourceId, targetId) {
  return `${sourceId}-${targetId}`;
 },

 getLinkDisplayKey(sourceId, targetId) {
  const sourceLogName = this.nodeStates[sourceId].logName;
  const targetLogName = this.nodeStates[targetId].logName;
  return `${sourceLogName} - ${targetLogName}`;
 },

 startNodeTransmission(nodeElement, d, mesh, highlightNodeAndLinks, unhighlightNodeAndLinks) {
  if(!this.nodeStates[d.id]) {
   log(`Initializing node state for ${this.nodeStates[d.id].logName}`);
   this.nodeStates[d.id] = {
    isTransmitting: false,
    hasTransmitted: false,
    delay: 0,
    role: d.role,
    txCount: 0,
    rxCount: 0,
    shortName: d.short_name,
    logName: `[${d.short_name} ${getNodeHexId(d.id)}]`,
    retryCount: 0,
    currentTimeout: null
   };
  }

  const nodeState = this.nodeStates[d.id];

  if(nodeState.hasTransmitted || nodeState.isTransmitting) {
   log(`Node ${nodeState.logName} already transmitted or is transmitting, skipping`);
   return;
  }

  if(!this.initialNode) {
   this.initialNode = d.id;
   log(`Initial node set to ${nodeState.logName}`);
  }

  const checkChannelAndStartBackoff = () => {
   const relevantLinks = mesh.links.filter(link => link.source === d.id || link.target === d.id);

   const busyLinks = relevantLinks.filter(link => {
    const linkKey = this.getLinkKey(link.source, link.target);
    return this.linkStates[linkKey].isBusy;
   }).length;

   const incomingLinks = relevantLinks.filter(link => link.target === d.id).length;
   const outgoingLinks = relevantLinks.filter(link => link.source === d.id).length;

   log(`Node ${nodeState.logName} channel busy on ${busyLinks}/${relevantLinks.length} links (${outgoingLinks} outgoing, ${incomingLinks} incoming)`);

   if(busyLinks === 0) {
    nodeState.delay = calculateBackoffDelay(nodeState.role);
    log(`Node ${nodeState.logName} (${nodeState.role}) starting backoff of ${Math.floor(nodeState.delay)} ms after channel became free`);

    if(nodeState.currentTimeout)
     clearTimeout(nodeState.currentTimeout);

    nodeState.currentTimeout = setTimeout(() => {
     if(nodeState.hasTransmitted || nodeState.isTransmitting) {
      log(`Node ${nodeState.logName} already transmitted or is transmitting, skipping`);
      return;
     }

     const busyLinksAfterBackoff = relevantLinks.filter(link => {
      const linkKey = this.getLinkKey(link.source, link.target);
      return this.linkStates[linkKey].isBusy;
     }).length;

     if(busyLinksAfterBackoff === 0)
      this.initiateTransmission(nodeElement, d, mesh, highlightNodeAndLinks, unhighlightNodeAndLinks, outgoingLinks);
     else
      this.retryTransmission(nodeElement, d, mesh, highlightNodeAndLinks, unhighlightNodeAndLinks);
    }, nodeState.delay);

    this.transmissionTimeouts.push(nodeState.currentTimeout);
   } else {
    setTimeout(checkChannelAndStartBackoff, 100);
   }
  };

  checkChannelAndStartBackoff();
 },

 initiateTransmission(nodeElement, d, mesh, highlightNodeAndLinks, unhighlightNodeAndLinks, totalOutgoingLinks) {
  const nodeState = this.nodeStates[d.id];

  if(nodeState.hasTransmitted || nodeState.isTransmitting) {
   log(`Node ${nodeState.logName} already transmitted or is transmitting`);
   return;
  }

  nodeState.isTransmitting = true;
  highlightNodeAndLinks(nodeElement, d, mesh.links);
  log(`Node ${nodeState.logName} TX started`);

  const outgoingLinks = mesh.links.filter(link => {
   return link.source === d.id && !this.nodeStates[link.target].hasTransmitted;
  });

  log(`Node ${nodeState.logName} transmitting through ${outgoingLinks.length} outgoing links`);

  outgoingLinks.forEach(link => {
   const linkKey = this.getLinkKey(link.source, link.target);
   this.linkStates[linkKey].isBusy = true;
   log(`Link ${this.getLinkDisplayKey(link.source, link.target)} marked as busy`);
  });

  const transmissionDelay = setTimeout(() => {
   nodeState.txCount++;
   this.completeTransmission(nodeElement, d, mesh, highlightNodeAndLinks, unhighlightNodeAndLinks);
  }, 1000);

  this.transmissionTimeouts.push(transmissionDelay);
 },

 completeTransmission(nodeElement, d, mesh, highlightNodeAndLinks, unhighlightNodeAndLinks) {
  const nodeState = this.nodeStates[d.id];
  nodeState.isTransmitting = false;
  nodeState.hasTransmitted = true;
  unhighlightNodeAndLinks(nodeElement, d, mesh.links);
  log(`Node ${nodeState.logName} TX completed, monitoring for repeated frames`);

  mesh.links.forEach(link => {
   const linkKey = this.getLinkKey(link.source, link.target);

   if(link.target !== d.id && this.linkStates[linkKey].isBusy) {
    this.linkStates[linkKey].isBusy = false;
    this.nodeStates[link.target].rxCount++;
    log(`Node ${this.nodeStates[link.target].logName} received a frame via link ${this.getLinkDisplayKey(link.source, link.target)}`);

    const targetNode = mesh.nodes.find(node => node.id === link.target);
    if(targetNode && !this.nodeStates[targetNode.id].hasTransmitted && targetNode.role !== "CLIENT_MUTE")
     this.startNodeTransmission(d3.select(`[data-id="${targetNode.id}"]`).node(), targetNode, mesh, highlightNodeAndLinks, unhighlightNodeAndLinks);
   }

   if(link.target === this.initialNode && link.source === d.id && this.nodeStates[this.initialNode].hasTransmitted) {
    this.nodeStates[this.initialNode].rxCount++;
    log(`Initial node ${this.nodeStates[this.initialNode].logName} received a frame back`);
   }

   this.linkStates[linkKey].isBusy = false;
  });

  this.checkAndRestartTransmission(mesh, highlightNodeAndLinks, unhighlightNodeAndLinks);
 },

 retryTransmission(nodeElement, d, mesh, highlightNodeAndLinks, unhighlightNodeAndLinks) {
  const nodeState = this.nodeStates[d.id];

  if(nodeState.currentTimeout)
   clearTimeout(nodeState.currentTimeout);

  if(nodeState.hasTransmitted) {
   log(`Node ${nodeState.logName} already transmitted, skipping retry`);
   return;
  }

  if(nodeState.retryCount >= this.maxRetries) {
   log(`Node ${nodeState.logName} reached max retries, stopping retransmission`);
   return;
  }

  nodeState.retryCount++;
  nodeState.delay = calculateBackoffDelay(nodeState.role);
  log(`Node ${nodeState.logName} retrying after ${Math.floor(nodeState.delay)} ms`);

  nodeState.currentTimeout = setTimeout(() => {
   this.startNodeTransmission(nodeElement, d, mesh, highlightNodeAndLinks, unhighlightNodeAndLinks);
  }, nodeState.delay);

  this.transmissionTimeouts.push(nodeState.currentTimeout);
 },

 checkAndRestartTransmission(mesh, highlightNodeAndLinks, unhighlightNodeAndLinks) {
  const pendingNodes = mesh.nodes.filter(node => {
   const hasIncomingLinks = mesh.links.some(link => link.target === node.id && this.nodeStates[link.source].hasTransmitted);
   return !this.nodeStates[node.id].hasTransmitted && node.role !== "CLIENT_MUTE" && hasIncomingLinks;
  });

  if(pendingNodes.length === 0) {
   log("All relevant nodes have transmitted, waiting 5 seconds before restarting simulation");

   this.restartTimeout = setTimeout(() => {
    mesh.nodes.forEach(node => {
     this.nodeStates[node.id].hasTransmitted = false;
     this.nodeStates[node.id].isTransmitting = false;
     this.nodeStates[node.id].txCount = 0;
     this.nodeStates[node.id].rxCount = 0;
     this.nodeStates[node.id].retryCount = 0;
     if(this.nodeStates[node.id].currentTimeout)
      clearTimeout(this.nodeStates[node.id].currentTimeout);
    });

    mesh.links.forEach(link => {
     const linkKey = this.getLinkKey(link.source, link.target);
     this.linkStates[linkKey].isBusy = false;
    });

    const initiator = mesh.nodes.find(node => node.id === this.initialNode);
    if(initiator) {
     log(`Restarting transmission from initial node ${this.nodeStates[this.initialNode].logName}`);
     this.startNodeTransmission(d3.select(`[data-id="${initiator.id}"]`).node(), initiator, mesh, highlightNodeAndLinks, unhighlightNodeAndLinks);
    }
   }, 5000);
  }
 },

 updateTooltip() {
  const tooltipElement = d3.select(".tooltip");

  if(tooltipElement.empty())
   return;

  let statsInfo = "<br>";
  const regularNodes = [];
  const clientMuteNodes = [];

  Object.keys(this.nodeStates).forEach(nodeId => {
   const state = this.nodeStates[nodeId];
   let backgroundColor = "#222";

   if(state.txCount > 0)
    backgroundColor = "#0c0";
   else if(state.retryCount > 0)
    backgroundColor = "#c00";
   else if(state.rxCount >= 1)
    backgroundColor = "#00f";

   const nodeInfo = `<span class="shortname" style="background-color:${backgroundColor};">${state.shortName}</span> ${state.retryCount} ${state.rxCount}<br>`;

   if(state.role === "CLIENT_MUTE")
    clientMuteNodes.push(nodeInfo);
   else
    regularNodes.push(nodeInfo);
  });

  statsInfo += "<div style=\"display: flex; gap: 10px;\">";
  statsInfo += `<div>${regularNodes.join("")}</div>`;
  statsInfo += `<div>${clientMuteNodes.join("")}</div>`;
  statsInfo += "</div>";

  let statsDiv = tooltipElement.select(".stats-info");
  if(statsDiv.empty())
   statsDiv = tooltipElement.append("div").attr("class", "stats-info");

  statsDiv.html(statsInfo);
 },

 stopCurrentAnimation(mesh) {
  this.transmissionTimeouts.forEach(timeout => clearTimeout(timeout));
  this.transmissionTimeouts = [];

  clearTimeout(this.restartTimeout);

  mesh.nodes.forEach(node => {
   const nodeState = this.nodeStates[node.id];
   nodeState.isTransmitting = false;
   nodeState.hasTransmitted = false;
   nodeState.txCount = 0;
   nodeState.rxCount = 0;
   nodeState.retryCount = 0;
   if(nodeState.currentTimeout)
    clearTimeout(nodeState.currentTimeout);
  });

  mesh.links.forEach(link => {
   const linkKey = this.getLinkKey(link.source, link.target);
   const linkState = this.linkStates[linkKey];
   linkState.isBusy = false;
   linkState.lastTransmissionTime = 0;
   linkState.receptionConfirmed = false;
  });

  this.activeNodes = [];
  log("Simulation stopped and reset");
 }
};

setInterval(() => animationManager.updateTooltip(), 50);