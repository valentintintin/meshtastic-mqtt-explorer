class Animation {
    constructor(nodeElement, nodeData, meshData, highlightNodeAndLinks, unhighlightNodeAndLinks) {
        this.nodeElement = nodeElement;
        this.nodeData = nodeData;
        this.meshData = meshData;
        this.highlightNodeAndLinks = highlightNodeAndLinks;
        this.unhighlightNodeAndLinks = unhighlightNodeAndLinks;
        this.blinkTimeouts = [];
    }

    triggerBlinkEffect() {
        this.clearAllBlinkTimeouts();
        let visitedNodes = new Set();
        let currentLevel = [this.nodeData];
        let delay = 500;

        const highlightLevel = (nodes) => {
            nodes.forEach(node => {
                if(node.role !== "CLIENT_MUTE" || node.id === this.nodeData.id) {
                    let element = d3.select(`[data-id="${node.id}"]`).node();
                    this.highlightNodeAndLinks(element, node, this.meshData.links);
                    visitedNodes.add(node.id);
                }
            });
        };

        const unhighlightLevel = (nodes) => {
            nodes.forEach(node => {
                let element = d3.select(`[data-id="${node.id}"]`).node();
                this.unhighlightNodeAndLinks(element, node, this.meshData.links);
            });
        };

        const propagate = (nodes) => {
            if(nodes.length === 0) {
                let timeout = setTimeout(() => {
                    unhighlightLevel([this.nodeData]);
                    visitedNodes.clear();
                    currentLevel = [this.nodeData];
                    propagate(currentLevel);
                }, delay);
                this.blinkTimeouts.push(timeout);
                return;
            }

            highlightLevel(nodes);

            let timeout = setTimeout(() => {
                unhighlightLevel(nodes);

                let nextLevel = [];

                nodes.forEach(node => {
                    let connectedNodes = this.meshData.links
                        .filter(link => {
                            if(node.role === "CLIENT_MUTE" && node.id !== this.nodeData.id)
                                return false;
                            return (link.source.id === node.id && !visitedNodes.has(link.target.id) && (link.bidirectional || !link.unidirectional)) ||
                                (link.target.id === node.id && !visitedNodes.has(link.source.id) && link.bidirectional);
                        })
                        .map(link => {
                            if(link.bidirectional)
                                return [link.source, link.target];
                            else
                                return link.source.id === node.id ? link.target : null;
                        })
                        .flat()
                        .filter(node => node !== null);

                    nextLevel.push(...connectedNodes);
                });

                nextLevel = nextLevel.filter((node, index, self) => {
                    return !visitedNodes.has(node.id) && self.findIndex(n => n.id === node.id) === index;
                });

                currentLevel = nextLevel;
                propagate(currentLevel);
            }, delay);
            this.blinkTimeouts.push(timeout);
        };

        highlightLevel(currentLevel);
        propagate(currentLevel);
    }

    stopBlinking() {
        this.clearAllBlinkTimeouts();
        d3.selectAll(".node circle").attr("opacity", 0.5);
        d3.selectAll(".link").classed("full-bidirectional-highlight", false)
            .classed("inbound-unidirectional-highlight", false)
            .classed("outbound-unidirectional-highlight", false);
    }

    clearAllBlinkTimeouts() {
        this.blinkTimeouts.forEach(timeout => clearTimeout(timeout));
        this.blinkTimeouts = [];
    }
}

class AnimationManager {
    constructor() {
        this.selectedNode = null;
    }

    stopCurrentAnimation() {
        if(this.selectedNode && this.selectedNode.csmaInstance) {
            this.selectedNode.csmaInstance.stopBlinking();
            this.selectedNode = null;
        }
    }

    selectNode(nodeElement, nodeData, meshData, highlightNodeAndLinks, unhighlightNodeAndLinks) {
        this.stopCurrentAnimation();
        this.selectedNode = {
            element: nodeElement,
            data: nodeData,
            csmaInstance: new Animation(nodeElement, nodeData, meshData, highlightNodeAndLinks, unhighlightNodeAndLinks)
        };
        this.selectedNode.csmaInstance.triggerBlinkEffect();
    }
}

window.animationManager = new AnimationManager();