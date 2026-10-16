/**
* Enhanced Layout System with Print Production Terminology - CLEANED VERSION
*/

function debugBounds() {
    const manager = window.layoutManagers['material_0'];
    if (manager && manager.calcs) {
        const c = manager.calcs;
        console.log('=== BOUNDS DEBUG ===');
        console.log('Printable Width:', c.printableWidth + 'px');
        console.log('Printable Length:', c.printableLength + 'px');
        console.log('Items per row:', c.maxColCount);
        console.log('Rows:', c.maxRowCount);
        console.log('Item + gutter width:', c.itemWidthPx + c.xGutterPx + 'px');
        console.log('Item + gutter height:', c.itemLengthPx + c.yGutterPx + 'px');

        const up = manager.layout.up;
        const itemsPerRow = Math.min(up, c.maxColCount);
        const rowsNeeded = Math.ceil(up / itemsPerRow);
        const widthNeeded = (itemsPerRow * c.itemWidthPx) + ((itemsPerRow - 1) * c.xGutterPx);
        const heightNeeded = (rowsNeeded * c.itemLengthPx) + ((rowsNeeded - 1) * c.yGutterPx);

        console.log('=== FOR UP VALUE', up, '===');
        console.log('Layout:', itemsPerRow, 'x', rowsNeeded);
        console.log('Width needed:', widthNeeded + 'px', widthNeeded > c.printableWidth ? '❌ EXCEEDS' : '✅ fits');
        console.log('Height needed:', heightNeeded + 'px', heightNeeded > c.printableLength ? '❌ EXCEEDS' : '✅ fits');
    }
}

function debugDutchCut() {
    const manager = window.layoutManagers['material_0'];
    if (manager && manager.calcs) {
        const c = manager.calcs;
        const up = manager.layout.up;

        console.log('=== DUTCH CUT DEBUG ===');
        console.log('Dutch Cut enabled:', manager.layout.dutchCut);
        console.log('Requested up:', up);
        console.log('Standard max items:', c.standardMaxItems);
        console.log('Rotated max items:', c.rotatedMaxItems);
        console.log('Hard max items:', c.hardMaxItems);
        console.log('Dutch Cut strategy:', c.dutchCutStrategy);

        if (c.dutchCutLayout) {
            console.log('Dutch Cut layout:', c.dutchCutLayout);
        }

        console.log('Would exceed bounds (standard):', c.wouldExceedBounds(up));
        if (c.wouldExceedBoundsDutchCut) {
            console.log('Would exceed bounds (Dutch Cut):', c.wouldExceedBoundsDutchCut(up));
        }

        console.log('Show custom layout:', c.showCustomLayout);
    }
}

class Layout {
    constructor(data) {
        // Material dimensions
        this.materialLength = Number(data.MaterialLength) || 0;
        this.materialWidth = Number(data.MaterialWidth) || 0;

        // Item dimensions (original, unswapped)
        this.originalItemLength = Number(data.ItemLength) || 0;
        this.originalItemWidth = Number(data.ItemWidth) || 0;

        // Current item dimensions (may be swapped)
        this.itemLength = this.originalItemLength;
        this.itemWidth = this.originalItemWidth;

        // Print production settings (in inches)
        this.leftSideGuide = Number(data.LeftSideGuide) || 0;
        this.rightSideGuide = Number(data.RightSideGuide) || 0;
        this.xGutter = Number(data.XGutter) || 0;
        this.yGutter = Number(data.YGutter) || 0;
        this.gripper = Number(data.Gripper) || 0;
        this.tail = Number(data.Tail) || 0;

        // Layout settings
        this.quantity = Number(data.Quantity) || 1;
        this.up = data.Up ? Number(data.Up) : null;
        this.dutchCut = data.DutchCut === 'True' || data.DutchCut === true;

        // Stock type
        this.stockType = data.StockType || 'sheet'; // 'sheet' or 'roll'

        // Display settings
        this.pixelsPerUnit = 10; // Scale factor for display
        this.materialId = data.MaterialId || 'material_0';
        this.materialIdSel = data.CssSelector || `#${this.materialId}`;
        this.parentType = data.ParentType || 'Job';
    }

    resetDimensions() {
        this.itemLength = this.originalItemLength;
        this.itemWidth = this.originalItemWidth;
    }

    toggleDutchCut() {
        this.dutchCut = !this.dutchCut;
    }
}

class LayoutCalculations {
    constructor(layout) {
        this.layout = layout;
        this.pixelsPerUnit = layout.pixelsPerUnit;

        this.setItemDimensions();
        this.setPrintSettings();
        this.setMaterialDimensions();
        this.calculatePixelValues();
        this.calculatePrintableArea();
        this.calculateMaxItems();
        this.calculateSheetCount();

        this.updateMaterialDisplay();
    }

    setItemDimensions() {
        this.itemLengthUnit = this.layout.itemLength;
        this.itemWidthUnit = this.layout.itemWidth;
    }

    setPrintSettings() {
        this.leftSideGuideUnit = this.layout.leftSideGuide;
        this.rightSideGuideUnit = this.layout.rightSideGuide;
        this.xGutterUnit = this.layout.xGutter;
        this.yGutterUnit = this.layout.yGutter;
        this.gripperUnit = this.layout.gripper;
        this.tailUnit = this.layout.tail;
    }

    setMaterialDimensions() {
        this.materialWidthUnit = this.layout.materialWidth;
        this.materialLengthUnit = this.layout.materialLength;
        this.quantity = this.layout.quantity;
    }

    calculatePixelValues() {
        // Item pixels (no gutters included in base size)
        this.itemLengthPx = this.itemLengthUnit * this.pixelsPerUnit;
        this.itemWidthPx = this.itemWidthUnit * this.pixelsPerUnit;

        // Print settings pixels
        this.leftSideGuidePx = this.leftSideGuideUnit * this.pixelsPerUnit;
        this.rightSideGuidePx = this.rightSideGuideUnit * this.pixelsPerUnit;
        this.xGutterPx = this.xGutterUnit * this.pixelsPerUnit;
        this.yGutterPx = this.yGutterUnit * this.pixelsPerUnit;
        this.gripperPx = this.gripperUnit * this.pixelsPerUnit;
        this.tailPx = this.tailUnit * this.pixelsPerUnit;

        // Material pixels
        this.materialWidthPx = this.materialWidthUnit * this.pixelsPerUnit;
        this.materialLengthPx = this.materialLengthUnit * this.pixelsPerUnit;

        // Rotated item dimensions for Dutch Cut
        this.itemRotatedLengthPx = this.itemWidthPx;
        this.itemRotatedWidthPx = this.itemLengthPx;
    }

    calculatePrintableArea() {
        // Printable area considers side guides
        this.printableWidth = this.materialWidthPx - this.leftSideGuidePx - this.rightSideGuidePx;

        // For sheet stock, gripper/tail apply per sheet
        // For roll stock, gripper/tail apply only at beginning/end
        if (this.layout.stockType === 'sheet') {
            this.printableLength = this.materialLengthPx - this.gripperPx - this.tailPx;
        } else {
            // For roll stock, we don't subtract gripper/tail from printable area per "sheet"
            // since they only apply at the very beginning and end of the roll
            this.printableLength = this.materialLengthPx;
        }
    }

    // FIXED: Single calculateMaxItems method with consistent logic
    calculateMaxItems() {
        // Calculate standard orientation items
        this.maxColCount = this.calculateItemsInDimension(this.printableWidth, this.itemWidthPx, this.xGutterPx);
        this.maxRowCount = this.calculateItemsInDimension(this.printableLength, this.itemLengthPx, this.yGutterPx);
        this.standardMaxItems = this.maxColCount * this.maxRowCount;

        // Calculate rotated orientation items (for Dutch Cut)
        this.maxColCountRotated = this.calculateItemsInDimension(this.printableWidth, this.itemRotatedWidthPx, this.xGutterPx);
        this.maxRowCountRotated = this.calculateItemsInDimension(this.printableLength, this.itemRotatedLengthPx, this.yGutterPx);
        this.rotatedMaxItems = this.maxColCountRotated * this.maxRowCountRotated;

        // Handle different stock types
        if (this.layout.stockType === 'Roll Stock' || this.layout.stockType === 'Roll') {
            this.handleRollStockLayout();
        } else {
            // Sheet stock logic
            if (this.layout.dutchCut) {
                this.calculateDutchCutLayout();
            } else {
                // MAS-39: Use whichever orientation yields the most up for sheet stock
                if (this.rotatedMaxItems > this.standardMaxItems) {
                    this.hardMaxItems = this.rotatedMaxItems;
                    this.useRotatedItems = true;
                } else {
                    this.hardMaxItems = this.standardMaxItems;
                    this.useRotatedItems = false;
                }
                this.dutchCutStrategy = 'standard-only';
            }
        }

        // Use the "up" value if specified, otherwise use calculated max
        this.softMaxItems = this.layout.up || this.hardMaxItems;

        // FIXED: Consistent bounds checking
        this.showCustomLayout = this.layout.up && this.exceedsLayoutBounds(this.layout.up);
    }

    // FIXED: Simplified and consistent item calculation
    calculateItemsInDimension(availableSpace, itemSize, gutterSize) {
        if (availableSpace < itemSize) return 0;

        // First item takes itemSize, each additional item takes itemSize + gutterSize
        let remainingSpace = availableSpace - itemSize;
        let additionalItems = 0;

        while (remainingSpace >= (itemSize + gutterSize)) {
            additionalItems++;
            remainingSpace -= (itemSize + gutterSize);
        }

        const totalItems = 1 + additionalItems;

        // Verify the total space needed doesn't exceed available space
        const totalSpaceNeeded = (totalItems * itemSize) + ((totalItems - 1) * gutterSize);

        if (totalSpaceNeeded > availableSpace + 0.1) { // Small tolerance for floating point
            console.warn(`Boundary check failed: need ${totalSpaceNeeded}px, have ${availableSpace}px`);
            return Math.max(0, totalItems - 1);
        }

        return totalItems;
    }

    // FIXED: Unified bounds checking method
    exceedsLayoutBounds(requestedUp) {
        if (this.layout.dutchCut) {
            return this.exceedsDutchCutBounds(requestedUp);
        }

        // MAS-39: Use rotated dimensions when rotated orientation yields more up
        const colCount = this.useRotatedItems ? this.maxColCountRotated : this.maxColCount;
        const itemW = this.useRotatedItems ? this.itemRotatedWidthPx : this.itemWidthPx;
        const itemL = this.useRotatedItems ? this.itemRotatedLengthPx : this.itemLengthPx;

        // Standard layout bounds check
        const itemsPerRow = Math.min(requestedUp, colCount);
        const rowsNeeded = Math.ceil(requestedUp / itemsPerRow);

        const totalWidthNeeded = (itemsPerRow * itemW) + ((itemsPerRow - 1) * this.xGutterPx);
        const totalHeightNeeded = (rowsNeeded * itemL) + ((rowsNeeded - 1) * this.yGutterPx);

        const widthExceeds = totalWidthNeeded > this.printableWidth;
        const heightExceeds = totalHeightNeeded > this.printableLength;

        if (widthExceeds || heightExceeds) {
            console.log(`Layout bounds exceeded for ${requestedUp} items:`);
            console.log(`  Width: ${totalWidthNeeded}px needed, ${this.printableWidth}px available ${widthExceeds ? '❌' : '✅'}`);
            console.log(`  Height: ${totalHeightNeeded}px needed, ${this.printableLength}px available ${heightExceeds ? '❌' : '✅'}`);
            return true;
        }

        return false;
    }

    // FIXED: Simplified Dutch Cut bounds checking
    exceedsDutchCutBounds(requestedUp) {
        // If we have a calculated Dutch Cut layout and it can handle this many items, it fits
        if (this.dutchCutLayout && requestedUp <= this.dutchCutLayout.totalItems) {
            return false;
        }

        // If requesting more items than our calculated max, it exceeds bounds
        if (requestedUp > this.hardMaxItems) {
            console.log(`Dutch Cut: ${requestedUp} items exceeds hard max of ${this.hardMaxItems}`);
            return true;
        }

        return false;
    }

    handleRollStockLayout() {
        // Calculate items that can fit across width
        this.itemsAcrossWidth = this.maxColCount;

        // For roll stock, we only show ONE row for display purposes
        this.linearItemsToShow = 1;

        // Calculate total length needed for the entire job
        const totalRowsNeeded = Math.ceil(this.quantity / this.itemsAcrossWidth);
        this.totalLinearLength = totalRowsNeeded * (this.itemLengthUnit + this.yGutterUnit) - this.yGutterUnit + this.gripperUnit + this.tailUnit;

        // For roll stock, hardMaxItems is the physical limit (items across width)
        this.hardMaxItems = this.itemsAcrossWidth;

        // But allow user override via "up" value (unless locked)
        this.softMaxItems = this.layout.up || this.itemsAcrossWidth;
        this.displayMaxItems = Math.min(this.softMaxItems, this.itemsAcrossWidth);

        this.isRollStock = true;
        this.useRotatedItems = false;

        // Show custom layout warning if up > hardMaxItems
        this.showCustomLayout = this.layout.up && this.layout.up > this.hardMaxItems;

        // Calculate Dutch Cut for roll stock if enabled
        if (this.layout.dutchCut) {
            this.calculateRollStockDutchCut();
        }
    }

    calculateRollStockDutchCut() {
        // For roll stock Dutch Cut, compare items across width in different orientations
        const standardAcrossWidth = this.maxColCount;
        const rotatedAcrossWidth = this.maxColCountRotated;

        if (rotatedAcrossWidth > standardAcrossWidth) {
            // All rotated is better
            this.itemsAcrossWidth = rotatedAcrossWidth;
            this.useRotatedItems = true;
            this.dutchCutStrategy = 'rotated-only';
        } else {
            // Try mixed layout - standard plus rotated in remaining width
            const usedWidth = standardAcrossWidth * (this.itemWidthPx + this.xGutterPx) - this.xGutterPx;
            const remainingWidth = this.printableWidth - usedWidth;

            if (remainingWidth >= this.itemRotatedWidthPx) {
                const additionalRotatedCols = Math.floor(remainingWidth / (this.itemRotatedWidthPx + this.xGutterPx));
                const totalAcrossWidth = standardAcrossWidth + additionalRotatedCols;

                if (totalAcrossWidth > this.itemsAcrossWidth) {
                    this.itemsAcrossWidth = totalAcrossWidth;
                    this.useRotatedItems = true;
                    this.dutchCutStrategy = 'mixed-right';
                    this.dutchCutStandardItems = standardAcrossWidth;
                    this.dutchCutRotatedItems = additionalRotatedCols;
                }
            }
        }

        // For roll stock, hardMaxItems and displayMaxItems are items across width (one row)
        this.hardMaxItems = this.itemsAcrossWidth;
        this.displayMaxItems = this.itemsAcrossWidth;

        // Recalculate total length with new layout
        const totalRowsNeeded = Math.ceil(this.quantity / this.itemsAcrossWidth);
        this.totalLinearLength = totalRowsNeeded * (this.itemLengthUnit + this.yGutterUnit) - this.yGutterUnit + this.gripperUnit + this.tailUnit;
    }

    // FIXED: Simplified Dutch Cut layout calculation
    calculateDutchCutLayout() {
        // Start with standard orientation as the base
        let bestLayout = {
            standardItems: this.standardMaxItems,
            rotatedItems: 0,
            totalItems: this.standardMaxItems,
            strategy: 'standard-only'
        };

        // Try pure rotated orientation
        if (this.rotatedMaxItems > bestLayout.totalItems) {
            bestLayout = {
                standardItems: 0,
                rotatedItems: this.rotatedMaxItems,
                totalItems: this.rotatedMaxItems,
                strategy: 'rotated-only'
            };
        }

        // Try mixed layout (standard + rotated in remaining space)
        const mixedLayout = this.calculateMixedLayout();
        if (mixedLayout && mixedLayout.totalItems > bestLayout.totalItems) {
            bestLayout = mixedLayout;
        }

        // Store the best layout
        this.dutchCutStandardItems = bestLayout.standardItems;
        this.dutchCutRotatedItems = bestLayout.rotatedItems;
        this.dutchCutStrategy = bestLayout.strategy;
        this.dutchCutLayout = bestLayout;
        this.hardMaxItems = bestLayout.totalItems;
        this.useRotatedItems = bestLayout.rotatedItems > 0;

        console.log('Dutch Cut Layout:', bestLayout);
    }

    calculateMixedLayout() {
        // Calculate remaining space after standard items
        const usedWidth = this.maxColCount * (this.itemWidthPx + this.xGutterPx) - this.xGutterPx;
        const usedHeight = this.maxRowCount * (this.itemLengthPx + this.yGutterPx) - this.yGutterPx;

        const remainingWidth = this.printableWidth - usedWidth;
        const remainingHeight = this.printableLength - usedHeight;

        let bestMixed = null;

        // Try rotated items in remaining width (right side)
        if (remainingWidth >= this.itemRotatedWidthPx) {
            const rotatedColsInWidth = this.calculateItemsInDimension(remainingWidth, this.itemRotatedWidthPx, this.xGutterPx);
            const rotatedRowsForFullHeight = this.maxRowCountRotated;
            const rotatedItemsInWidth = rotatedColsInWidth * rotatedRowsForFullHeight;

            if (rotatedItemsInWidth > 0) {
                const totalItems = this.standardMaxItems + rotatedItemsInWidth;
                bestMixed = {
                    standardItems: this.standardMaxItems,
                    rotatedItems: rotatedItemsInWidth,
                    totalItems: totalItems,
                    strategy: 'mixed-right',
                    rotatedCols: rotatedColsInWidth,
                    rotatedRows: rotatedRowsForFullHeight,
                    rotatedStartX: usedWidth + this.xGutterPx
                };
            }
        }

        // Try rotated items in remaining height (bottom)
        if (remainingHeight >= this.itemRotatedLengthPx) {
            const rotatedRowsInHeight = this.calculateItemsInDimension(remainingHeight, this.itemRotatedLengthPx, this.yGutterPx);
            const rotatedColsForFullWidth = this.maxColCountRotated;
            const rotatedItemsInHeight = rotatedRowsInHeight * rotatedColsForFullWidth;

            if (rotatedItemsInHeight > 0) {
                const totalItems = this.standardMaxItems + rotatedItemsInHeight;
                if (!bestMixed || totalItems > bestMixed.totalItems) {
                    bestMixed = {
                        standardItems: this.standardMaxItems,
                        rotatedItems: rotatedItemsInHeight,
                        totalItems: totalItems,
                        strategy: 'mixed-bottom',
                        rotatedCols: rotatedColsForFullWidth,
                        rotatedRows: rotatedRowsInHeight,
                        rotatedStartY: usedHeight + this.yGutterPx
                    };
                }
            }
        }

        return bestMixed;
    }

    calculateSheetCount() {
        if (this.layout.stockType === 'roll' || this.layout.stockType === 'Roll Stock' || this.layout.stockType === 'Roll') {
            // For roll stock, calculate based on items per row (could be overridden by up value)
            const itemsPerRow = this.layout.up || this.itemsAcrossWidth;
            this.calculatedSheetCount = Math.ceil(this.quantity / itemsPerRow);
            this.lastSheetItemCount = 0; // Not applicable for roll stock
        } else {
            // For sheet stock, calculate based on items per sheet
            this.calculatedSheetCount = Math.ceil(this.quantity / this.softMaxItems);
            this.lastSheetItemCount = this.calculatedSheetCount > 1 ? this.quantity % this.softMaxItems : 0;
        }

        // Update UI elements
        const sheetCountElement = document.getElementById(`dbg_num_sheets_${this.layout.materialId}`);
        if (sheetCountElement) {
            if (this.layout.stockType === 'roll' || this.layout.stockType === 'Roll Stock' || this.layout.stockType === 'Roll') {
                sheetCountElement.textContent = `${this.calculatedSheetCount} rows`;
            } else {
                sheetCountElement.textContent = this.calculatedSheetCount;
            }
        }

        const upInput = document.getElementById(`up_${this.layout.materialId}`);
        if (upInput) {
            // Set the current up value (user can change this)
            upInput.value = this.layout.up || this.hardMaxItems;
            upInput.setAttribute('data-max-up', this.hardMaxItems);

            // Disable if UpValueLocked is true
            //const upValueLocked = $('.up-val-locked').val() === 'True' || $('.up-val-locked').val() === 'true';
            //upInput.disabled = upValueLocked;
        }
    }

    updateMaterialDisplay() {
        const material = document.querySelector(this.layout.materialIdSel);
        if (material) {
            // FIXED: Force exact dimensions and remove any CSS that might override
            const targetWidth = this.materialWidthPx;
            const targetHeight = this.materialLengthPx;

            // For roll stock, show a compact height that includes one row + guides
            if (this.layout.stockType === 'Roll Stock' || this.layout.stockType === 'Roll') {
                // Calculate height for one row of items plus gripper and tail
                const oneRowHeight = this.gripperPx + this.itemLengthPx + this.tailPx + 20; // 20px padding for visual clarity
                material.style.height = oneRowHeight + 'px';
                material.classList.add('roll-stock');
                // Different styling for roll stock to show it's continuous
                material.style.setProperty('border-left', '4px solid #ff6b35', 'important');
                material.style.setProperty('border-right', '4px solid #ff6b35', 'important');
            } else {
                material.style.height = targetHeight + 'px';
                material.classList.remove('roll-stock');
                // Regular sheet stock border
                material.style.setProperty('border', '2px solid #333333', 'important');
            }

            material.style.width = targetWidth + 'px';

            // FIXED: Force these styles to override any CSS
            material.style.setProperty('width', targetWidth + 'px', 'important');
            material.style.setProperty('height', (this.layout.stockType === 'Roll Stock' || this.layout.stockType === 'Roll' ?
                (this.gripperPx + this.itemLengthPx + this.tailPx + 20) : targetHeight) + 'px', 'important');
            material.style.setProperty('box-sizing', 'border-box', 'important');
            material.style.setProperty('padding', '0', 'important');
            material.style.setProperty('position', 'relative', 'important');
            material.style.setProperty('background-color', '#ffffff', 'important');
            material.style.setProperty('border', '2px solid #333333', 'important');
            //material.style.setProperty('margin', '0', 'important');

            // Debug output to verify
            console.log(`Material display updated: ${targetWidth}px × ${targetHeight}px`);
            console.log(`Actual rendered size will be: ${material.offsetWidth}px × ${material.offsetHeight}px`);
        }

        const customMaterial = document.getElementById(`custom_${this.layout.materialId}`);
        if (customMaterial) {
            const targetHeight = this.materialLengthPx;

            if (this.layout.stockType === 'Roll Stock' || this.layout.stockType === 'Roll') {
                const oneRowHeight = this.gripperPx + this.itemLengthPx + this.tailPx + 20;
                customMaterial.style.height = oneRowHeight + 'px';
                customMaterial.style.setProperty('height', oneRowHeight + 'px', 'important');
            } else {
                customMaterial.style.height = targetHeight + 'px';
                customMaterial.style.setProperty('height', targetHeight + 'px', 'important');
            }
        }
    }
}

// Continue with LayoutManager class... (keeping the same as original for now)
class LayoutManager {
    constructor(layout) {
        this.layout = layout;
        this.recalculate();
    }

    recalculate() {
        this.calcs = new LayoutCalculations(this.layout);
        this.clearLayout();
        this.drawLayout();
    }

    clearLayout() {
        const material = document.querySelector(this.layout.materialIdSel);
        if (material) {
            material.innerHTML = '';
        }
    }

    drawLayout() {
        const material = document.querySelector(this.layout.materialIdSel);
        if (!material) return;

        // Remove padding since we're positioning items absolutely with proper offsets
        material.style.padding = '0';
        // CRITICAL FIX: Set position relative to establish positioning context for absolute items
        material.style.position = 'relative';

        // Add sheet boundary visualization
        material.style.backgroundColor = '#ffffff';
        material.style.border = '2px solid #333333';
        material.style.boxShadow = '0 4px 8px rgba(0,0,0,0.1)';
        material.style.margin = '20px auto';
        material.style.display = 'block';

        // Add visual guides
        this.drawGuides();

        if (this.calcs.isRollStock) {
            this.drawRollStockLayout();
        } else {
            let itemsDrawn = 0;
            const itemsToDraw = Math.min(this.layout.quantity, this.calcs.softMaxItems);

            if (this.layout.dutchCut && this.calcs.useRotatedItems) {
                itemsDrawn = this.drawDutchCutLayout(itemsToDraw);
            } else {
                itemsDrawn = this.drawStandardLayout(itemsToDraw);
            }
        }

        // Handle custom layout display
        this.handleCustomLayout();
    }

    // FIXED: More accurate bounds checking in item creation
    createStandardItem(row, col) {
        // MAS-39: Use rotated dimensions when rotated orientation yields more up
        const itemW = this.calcs.useRotatedItems ? this.calcs.itemRotatedWidthPx : this.calcs.itemWidthPx;
        const itemL = this.calcs.useRotatedItems ? this.calcs.itemRotatedLengthPx : this.calcs.itemLengthPx;

        const itemDiv = document.createElement('div');
        itemDiv.className = 'item-region';
        itemDiv.style.position = 'absolute';
        itemDiv.style.height = itemL + 'px';
        itemDiv.style.width = itemW + 'px';
        itemDiv.style.backgroundColor = '#e3f2fd';
        itemDiv.style.border = '1px solid #1976d2';
        itemDiv.style.boxSizing = 'border-box';
        itemDiv.style.zIndex = '2'; // Above guides but below UI elements

        // Calculate position with gutters - OFFSET by side guides and gripper/tail
        const xPos = this.calcs.leftSideGuidePx + (col * (itemW + this.calcs.xGutterPx));
        const yPos = this.calcs.gripperPx + (row * (itemL + this.calcs.yGutterPx));

        // FIXED: More precise bounds checking
        const itemRight = xPos + itemW;
        const itemBottom = yPos + itemL;
        const materialRight = this.calcs.materialWidthPx - this.calcs.rightSideGuidePx;
        const materialBottom = this.calcs.materialLengthPx - this.calcs.tailPx;

        // Allow small tolerance for floating point precision
        const tolerance = 0.5;

        if (itemRight > materialRight + tolerance || itemBottom > materialBottom + tolerance) {
            itemDiv.style.backgroundColor = '#ffebee';
            itemDiv.style.border = '2px solid #f44336';
            itemDiv.style.opacity = '0.7';
            itemDiv.title = 'This item exceeds the printable area';
            console.warn(`Item at ${row},${col} exceeds bounds: right=${itemRight}px (max=${materialRight}px), bottom=${itemBottom}px (max=${materialBottom}px)`);
        }

        itemDiv.style.left = xPos + 'px';
        itemDiv.style.top = yPos + 'px';

        return itemDiv;
    }

    drawRollStockLayout() {
        const material = document.querySelector(this.layout.materialIdSel);
        let itemsDrawn = 0;

        // Only draw one row of items
        const itemsInOneRow = this.calcs.itemsAcrossWidth;
        const itemsToShow = Math.min(this.layout.quantity, itemsInOneRow);

        // Add roll stock indicator
        this.addRollStockIndicator();

        if (this.calcs.useRotatedItems && this.calcs.dutchCutStrategy === 'rotated-only') {
            // All items rotated - just one row
            for (let col = 0; col < this.calcs.itemsAcrossWidth && itemsDrawn < itemsToShow; col++) {
                const itemDiv = this.createRotatedItem(0, col); // row = 0 (only one row)
                itemDiv.classList.add('item', 'rotated-item');
                material.appendChild(itemDiv);
                itemsDrawn++;
            }
        } else if (this.calcs.useRotatedItems && this.calcs.dutchCutStrategy === 'mixed-right') {
            // Mixed layout for roll stock - one row only
            // Standard items
            for (let col = 0; col < this.calcs.dutchCutStandardItems && itemsDrawn < itemsToShow; col++) {
                const itemDiv = this.createStandardItem(0, col); // row = 0 (only one row)
                itemDiv.classList.add('item');
                material.appendChild(itemDiv);
                itemsDrawn++;
            }
            // Rotated items in remaining width - one row only
            const startX = this.calcs.dutchCutStandardItems * (this.calcs.itemWidthPx + this.calcs.xGutterPx);
            for (let col = 0; col < this.calcs.dutchCutRotatedItems && itemsDrawn < itemsToShow; col++) {
                const itemDiv = this.createRotatedItemInSpace(0, col, startX, 0); // row = 0 (only one row)
                itemDiv.classList.add('item', 'rotated-item');
                material.appendChild(itemDiv);
                itemsDrawn++;
            }
        } else {
            // Standard layout - one row only
            for (let col = 0; col < this.calcs.itemsAcrossWidth && itemsDrawn < itemsToShow; col++) {
                const itemDiv = this.createStandardItem(0, col); // row = 0 (only one row)
                itemDiv.classList.add('item');
                material.appendChild(itemDiv);
                itemsDrawn++;
            }
        }
    }

    addRollStockIndicator() {
        const material = document.querySelector(this.layout.materialIdSel);

        // Remove any existing indicator
        const existingIndicator = material.querySelector('.roll-stock-indicator');
        if (existingIndicator) {
            existingIndicator.remove();
        }

        // Calculate efficiency metrics
        const totalRowsNeeded = Math.ceil(this.layout.quantity / this.calcs.itemsAcrossWidth);

        // Create roll stock info overlay
        const indicator = document.createElement('div');
        indicator.className = 'roll-stock-indicator';
        indicator.innerHTML = `
        <div class="roll-info">
            <h5>📜 Roll Stock Layout (One Row Sample)</h5>
            <div class="roll-metrics">
                <div><strong>Items Per Row:</strong> ${this.calcs.itemsAcrossWidth}</div>
                <div><strong>Total Rows Needed:</strong> ${totalRowsNeeded}</div>
                <div><strong>Total Linear Length:</strong> ${this.calcs.totalLinearLength.toFixed(2)}"</div>
                <div><strong>Quantity:</strong> ${this.layout.quantity} items</div>
            </div>
            <div class="layout-pattern">
                <small>⬆️ Pattern shown above repeats ${totalRowsNeeded} time${totalRowsNeeded !== 1 ? 's' : ''} along the roll</small>
            </div>
        </div>
    `;

        // Position the indicator in the bottom right corner
        indicator.style.cssText = `
        position: absolute;
        bottom: 10px;
        right: 10px;
        background: rgba(255, 255, 255, 0.95);
        border: 1px solid #ddd;
        border-radius: 8px;
        padding: 12px;
        font-size: 12px;
        box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        max-width: 250px;
        z-index: 10;
    `;

        material.appendChild(indicator);
    }

    drawGuides() {
        const material = document.querySelector(this.layout.materialIdSel);

        // Add guide visual indicators
        const guideStyle = 'position: absolute; background-color: rgba(255, 0, 0, 0.1); border: 1px dashed red; z-index: 1;';

        // Left side guide
        if (this.calcs.leftSideGuidePx > 0) {
            const leftGuide = document.createElement('div');
            leftGuide.style.cssText = guideStyle + `left: 0; top: 0; width: ${this.calcs.leftSideGuidePx}px; height: 100%;`;
            leftGuide.className = 'guide-left';
            material.appendChild(leftGuide);
        }

        // Right side guide
        if (this.calcs.rightSideGuidePx > 0) {
            const rightGuide = document.createElement('div');
            rightGuide.style.cssText = guideStyle + `right: 0; top: 0; width: ${this.calcs.rightSideGuidePx}px; height: 100%;`;
            rightGuide.className = 'guide-right';
            material.appendChild(rightGuide);
        }

        // Gripper
        if (this.calcs.gripperPx > 0) {
            const gripper = document.createElement('div');
            gripper.style.cssText = guideStyle + `left: 0; top: 0; width: 100%; height: ${this.calcs.gripperPx}px;`;
            gripper.className = 'guide-gripper';
            material.appendChild(gripper);
        }

        // Tail
        if (this.calcs.tailPx > 0) {
            const tail = document.createElement('div');
            tail.style.cssText = guideStyle + `left: 0; bottom: 0; width: 100%; height: ${this.calcs.tailPx}px;`;
            tail.className = 'guide-tail';
            material.appendChild(tail);
        }
    }

    drawStandardLayout(itemsToDraw) {
        const material = document.querySelector(this.layout.materialIdSel);
        let itemsDrawn = 0;

        // MAS-39: Use rotated counts when rotated orientation yields more up
        const rowCount = this.calcs.useRotatedItems ? this.calcs.maxRowCountRotated : this.calcs.maxRowCount;
        const colCount = this.calcs.useRotatedItems ? this.calcs.maxColCountRotated : this.calcs.maxColCount;

        for (let row = 0; row < rowCount && itemsDrawn < itemsToDraw; row++) {
            for (let col = 0; col < colCount && itemsDrawn < itemsToDraw; col++) {
                const itemDiv = this.createStandardItem(row, col);
                itemDiv.classList.add('item');
                material.appendChild(itemDiv);
                itemsDrawn++;
            }
        }

        return itemsDrawn;
    }

    drawDutchCutLayout(itemsToDraw) {
        const material = document.querySelector(this.layout.materialIdSel);
        let itemsDrawn = 0;

        if (this.calcs.dutchCutStrategy === 'rotated-only') {
            // All items are rotated
            for (let row = 0; row < this.calcs.maxRowCountRotated && itemsDrawn < itemsToDraw; row++) {
                for (let col = 0; col < this.calcs.maxColCountRotated && itemsDrawn < itemsToDraw; col++) {
                    const itemDiv = this.createRotatedItem(row, col);
                    itemDiv.classList.add('item', 'rotated-item');
                    material.appendChild(itemDiv);
                    itemsDrawn++;
                }
            }
        } else if (this.calcs.dutchCutStrategy === 'standard-only') {
            // All items are standard orientation
            itemsDrawn = this.drawStandardLayout(itemsToDraw);
        } else {
            // Mixed layout - draw standard items first, then rotated in remaining space
            const standardItemsToDraw = Math.min(this.calcs.dutchCutStandardItems, itemsToDraw);

            // Draw standard items
            for (let row = 0; row < this.calcs.maxRowCount && itemsDrawn < standardItemsToDraw; row++) {
                for (let col = 0; col < this.calcs.maxColCount && itemsDrawn < standardItemsToDraw; col++) {
                    const itemDiv = this.createStandardItem(row, col);
                    itemDiv.classList.add('item');
                    material.appendChild(itemDiv);
                    itemsDrawn++;
                }
            }

            // Draw rotated items in remaining space
            const remainingItemsToDraw = itemsToDraw - itemsDrawn;
            if (remainingItemsToDraw > 0 && this.calcs.dutchCutRotatedItems > 0) {
                itemsDrawn += this.drawRotatedInRemainingSpace(itemsDrawn, remainingItemsToDraw);
            }
        }

        return itemsDrawn;
    }

    drawRotatedInRemainingSpace(startingCount, remainingItemsToDraw) {
        const material = document.querySelector(this.layout.materialIdSel);
        let rotatedItemsDrawn = 0;
        const rotatedItemsToDraw = Math.min(this.calcs.dutchCutRotatedItems, remainingItemsToDraw);
        const layout = this.calcs.dutchCutLayout;

        if (layout.strategy === 'mixed-right') {
            // Draw rotated items in the remaining width (right side)
            for (let row = 0; row < layout.rotatedRows && rotatedItemsDrawn < rotatedItemsToDraw; row++) {
                for (let col = 0; col < layout.rotatedCols && rotatedItemsDrawn < rotatedItemsToDraw; col++) {
                    const itemDiv = this.createRotatedItemInSpace(row, col, layout.rotatedStartX, 0);
                    itemDiv.classList.add('item', 'rotated-item');
                    material.appendChild(itemDiv);
                    rotatedItemsDrawn++;
                }
            }
        } else if (layout.strategy === 'mixed-bottom') {
            // Draw rotated items in the remaining height (bottom)
            for (let row = 0; row < layout.rotatedRows && rotatedItemsDrawn < rotatedItemsToDraw; row++) {
                for (let col = 0; col < layout.rotatedCols && rotatedItemsDrawn < rotatedItemsToDraw; col++) {
                    const itemDiv = this.createRotatedItemInSpace(row, col, 0, layout.rotatedStartY);
                    itemDiv.classList.add('item', 'rotated-item');
                    material.appendChild(itemDiv);
                    rotatedItemsDrawn++;
                }
            }
        }

        return rotatedItemsDrawn;
    }

    createRotatedItemInSpace(row, col, offsetX, offsetY) {
        const itemDiv = document.createElement('div');
        itemDiv.className = 'item-region rotated-item';
        itemDiv.style.position = 'absolute';
        itemDiv.style.height = this.calcs.itemRotatedLengthPx + 'px';
        itemDiv.style.width = this.calcs.itemRotatedWidthPx + 'px';
        itemDiv.style.backgroundColor = '#fff3e0';
        itemDiv.style.border = '1px solid #f57c00';
        itemDiv.style.boxSizing = 'border-box';
        itemDiv.style.opacity = '0.8';

        // Calculate position with gutters and offsets
        const xPos = this.calcs.leftSideGuidePx + offsetX + (col * (this.calcs.itemRotatedWidthPx + this.calcs.xGutterPx));
        const yPos = this.calcs.gripperPx + offsetY + (row * (this.calcs.itemRotatedLengthPx + this.calcs.yGutterPx));

        itemDiv.style.left = xPos + 'px';
        itemDiv.style.top = yPos + 'px';

        return itemDiv;
    }

    createRotatedItem(row, col) {
        const itemDiv = document.createElement('div');
        itemDiv.className = 'item-region rotated-item';
        itemDiv.style.position = 'absolute';
        itemDiv.style.height = this.calcs.itemRotatedLengthPx + 'px';
        itemDiv.style.width = this.calcs.itemRotatedWidthPx + 'px';
        itemDiv.style.backgroundColor = '#fff3e0';
        itemDiv.style.border = '1px solid #f57c00';
        itemDiv.style.boxSizing = 'border-box';
        itemDiv.style.opacity = '0.8';

        // Calculate position with gutters - OFFSET by side guides and gripper/tail
        const xPos = this.calcs.leftSideGuidePx + (col * (this.calcs.itemRotatedWidthPx + this.calcs.xGutterPx));
        const yPos = this.calcs.gripperPx + (row * (this.calcs.itemRotatedLengthPx + this.calcs.yGutterPx));

        itemDiv.style.left = xPos + 'px';
        itemDiv.style.top = yPos + 'px';

        return itemDiv;
    }

    handleCustomLayout() {
        const customElement = document.getElementById(`custom_${this.layout.materialId}`);
        const materialElement = document.querySelector(this.layout.materialIdSel);

        if (this.calcs.showCustomLayout) {
            // Show bounds exceeded message with more detailed information
            if (customElement) {
                const maxPossible = this.calcs.hardMaxItems;
                const requested = this.layout.up;
                const printableWidth = (this.calcs.printableWidth / this.calcs.pixelsPerUnit).toFixed(2);
                const printableLength = (this.calcs.printableLength / this.calcs.pixelsPerUnit).toFixed(2);

                // MAS-39: Use rotated dimensions when rotated orientation yields more up
                const colCount = this.calcs.useRotatedItems ? this.calcs.maxColCountRotated : this.calcs.maxColCount;
                const itemW = this.calcs.useRotatedItems ? this.calcs.itemRotatedWidthPx : this.calcs.itemWidthPx;
                const itemL = this.calcs.useRotatedItems ? this.calcs.itemRotatedLengthPx : this.calcs.itemLengthPx;

                // Calculate what's actually happening with the requested layout
                const itemsPerRow = Math.min(requested, colCount);
                const rowsNeeded = Math.ceil(requested / itemsPerRow);
                const actualWidthNeeded = ((itemsPerRow * itemW) + ((itemsPerRow - 1) * this.calcs.xGutterPx)) / this.calcs.pixelsPerUnit;
                const actualHeightNeeded = ((rowsNeeded * itemL) + ((rowsNeeded - 1) * this.calcs.yGutterPx)) / this.calcs.pixelsPerUnit;

                const boundsMessage = `
                <div class="bounds-exceeded-message">
                    <h4>⚠️ Layout Exceeds Print Area</h4>
                    <p><strong>Requested:</strong> ${requested} items per sheet</p>
                    <p><strong>Maximum Possible:</strong> ${maxPossible} items per sheet</p>

                    <div class="bounds-analysis">
                        <h5>Space Analysis:</h5>
                        <p><strong>Calculated Layout:</strong> ${itemsPerRow} items × ${rowsNeeded} rows</p>
                        <p><strong>Width Required:</strong> ${actualWidthNeeded.toFixed(2)}" (Available: ${printableWidth}")</p>
                        <p><strong>Height Required:</strong> ${actualHeightNeeded.toFixed(2)}" (Available: ${printableLength}")</p>
                        ${actualWidthNeeded > parseFloat(printableWidth) ? '<p class="text-danger">❌ Width exceeds available space</p>' : '<p class="text-success">✅ Width fits</p>'}
                        ${actualHeightNeeded > parseFloat(printableLength) ? '<p class="text-danger">❌ Height exceeds available space</p>' : '<p class="text-success">✅ Height fits</p>'}
                    </div>

                    <div class="suggestion">
                        <strong>Suggestions:</strong><br>
                        • Reduce the "Up" value to ${maxPossible} or less<br>
                        • Decrease item dimensions<br>
                        • Reduce gutters or side guides<br>
                        • Consider a larger material size
                    </div>

                    <div class="bounds-details">
                        <small><strong>Current Constraints:</strong></small>
                        <ul>
                            <li><strong>Material Size:</strong> ${this.layout.materialWidth}" × ${this.layout.materialLength}"</li>
                            <li><strong>Printable Area:</strong> ${printableWidth}" × ${printableLength}"</li>
                            <li><strong>Item Size:</strong> ${this.layout.itemWidth}" × ${this.layout.itemLength}"</li>
                            <li><strong>Side Guides:</strong> Left: ${this.layout.leftSideGuide}", Right: ${this.layout.rightSideGuide}"</li>
                            <li><strong>Gutters:</strong> X: ${this.layout.xGutter}", Y: ${this.layout.yGutter}"</li>
                            <li><strong>Gripper/Tail:</strong> ${this.layout.gripper}" / ${this.layout.tail}"</li>
                        </ul>
                    </div>
                </div>
            `;
                customElement.innerHTML = boundsMessage;
                customElement.style.display = 'block';
            }
            if (materialElement) materialElement.style.display = 'none';
        } else {
            // Show normal layout
            if (customElement) customElement.style.display = 'none';
            if (materialElement) materialElement.style.display = 'block';
        }
    }

    toggleDutchCut(enabled) {
        this.layout.dutchCut = enabled;
        this.recalculate();

        // Update hidden input
        const dutchInput = document.querySelector('.dutch-cut');
        if (dutchInput) dutchInput.value = enabled.toString();
    }

    updateQuantity(newQuantity) {
        this.layout.quantity = Number(newQuantity) || 1;
        this.recalculate();
    }

    updateUp(newUp) {
        this.layout.up = Number(newUp) || null;
        this.recalculate();
    }

    updatePrintSettings(leftSideGuide, rightSideGuide, xGutter, yGutter, gripper, tail) {
        this.layout.leftSideGuide = Number(leftSideGuide) || 0;
        this.layout.rightSideGuide = Number(rightSideGuide) || 0;
        this.layout.xGutter = Number(xGutter) || 0;
        this.layout.yGutter = Number(yGutter) || 0;
        this.layout.gripper = Number(gripper) || 0;
        this.layout.tail = Number(tail) || 0;
        this.recalculate();
    }
}

function OnOffLinkerSideGuides() {
    const checkbox = $(this);
    const isChecked = checkbox.is(':checked');
    const materialId = checkbox.attr('id').replace('link_side_guides_', '');

    if (isChecked) {
        // Link the side guides - when one changes, update the other
        $(`#left_side_guide_material_0, #right_side_guide_material_0`).on('input.linked', function () {
            const value = $(this).val();
            $(`#left_side_guide_material_0, #right_side_guide_material_0`).not(this).val(value);

            // Trigger layout update
            const layoutManager = window.layoutManagers[materialId];
            if (layoutManager) {
                const xGutter = $(`#x_gutter_material_0`).val();
                const yGutter = $(`#y_gutter_material_0`).val();
                const gripper = $(`#gripper_material_0`).val();
                const tail = $(`#tail_material_0`).val();
                layoutManager.updatePrintSettings(value, value, xGutter, yGutter, gripper, tail);
                markAsChanged();
            }
        });

        // Sync the values immediately when linking is enabled
        const leftValue = $(`#left_side_guide_material_0`).val();
        const rightValue = $(`#right_side_guide_material_0`).val();

        // Use the non-zero value, or left if both are the same
        const valueToUse = leftValue !== '0' ? leftValue : rightValue;
        $(`#left_side_guide_material_0, #right_side_guide_material_0`).val(valueToUse);

        // Trigger layout update with synced values
        const layoutManager = window.layoutManagers[materialId];
        if (layoutManager) {
            const xGutter = $(`#x_gutter_material_0`).val();
            const yGutter = $(`#y_gutter_material_0`).val();
            const gripper = $(`#gripper_material_0`).val();
            const tail = $(`#tail_material_0`).val();
            layoutManager.updatePrintSettings(valueToUse, valueToUse, xGutter, yGutter, gripper, tail);
            markAsChanged();
        }
    } else {
        // Unlink the side guides
        $(`#left_side_guide_material_0, #right_side_guide_material_0`).off('input.linked');
    }
}

// Enhanced version that also handles loading saved link states
function initializeLinkStates(materialId, linkSideGuides = false, linkGutters = false) {
    // Set the checkbox states
    $(`#link_side_guides_material_0`).prop('checked', linkSideGuides);
    $(`#link_gutters_material_0`).prop('checked', linkGutters);

    // Trigger the linker functions to set up the event handlers
    if (linkSideGuides) {
        $(`#link_side_guides_material_0`).trigger('click');
    }

    if (linkGutters) {
        $(`#link_gutters_material_0`).trigger('click');
    }
}

// Function to get current link states for saving
function getCurrentLinkStates(materialId) {
    return {
        linkSideGuides: $(`#link_side_guides_material_0`).is(':checked'),
        linkGutters: $(`#link_gutters_material_0`).is(':checked')
    };
}

// Initialize global objects if they don't exist
window.layoutManagers = window.layoutManagers || {};
window.layouts = window.layouts || [];