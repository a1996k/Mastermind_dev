class Layout {
    constructor(theData) {
        this.mLength = Number(theData.MaterialLength);
        this.mWidth = Number(theData.MaterialWidth);
        this.iLength = Number(theData.ItemLength);
        this.iWidth = Number(theData.ItemWidth);
        this.marginXL = Number(theData.MarginXL);
        this.marginXR = Number(theData.MarginXR);
        this.marginYT = Number(theData.MarginYT);
        this.marginYB = Number(theData.MarginYB);
        this.sideGuides = Number(theData.SideGuides);
        this.gripper = Number(theData.Gripper);
        this.tail = Number(theData.Tail);
        this.pixelsPerUnit = 10; // currently not changeable by user
        this.quantity = Number(theData.Quantity);
        this.up = theData.Up ? Number(theData.Up) : undefined;
        this.stockType = theData.StockType;
        this.materialId = theData.MaterialId;
        this.materialIdSel = theData.CssSelector;
        this.showLast = theData.ShowLast ? theData.ShowLast : false;

        //console.log(this);
    }
}

class LayoutCalculations {
    constructor(layout) {
        this.layout = layout;
        this.isMixGrain = isMixedGrain[layout.materialId.split('_')[1]];
        this.pixelsPerUnit = layout.pixelsPerUnit;

        this.setItemDimensions(layout);
        this.setMargins(layout);
        this.setMaterialDimensions(layout);

        this.calculateItemPixels();
        this.calculateMaterialPixels();
        this.calculateDrawableArea();
        this.calculateMaxItems();

        this.calculateRotatedDimensions();

        $(this.layout.materialIdSel).css('width', this.materialWidthPx + 2);
        $(this.layout.materialIdSel).css('height', this.materialLengthPx + 2);
        $('#custom_' + this.layout.materialId).css('height', this.materialLengthPx + 2);
    }

    setItemDimensions(layout) {
        this.itemLengthUnit = layout.iLength;
        this.itemWidthUnit = layout.iWidth;
    }

    setMargins(layout) {
        this.marginXRUnit = layout.marginXR;
        this.marginXLUnit = layout.marginXL;
        this.marginYTUnit = layout.marginYT;
        this.marginYBUnit = layout.marginYB;
        this.sideGuidesUnit = layout.sideGuides;
        this.gripperUnit = layout.gripper;
        this.tailUnit = layout.tail;
    }

    setMaterialDimensions(layout) {
        this.materialWidthUnit = layout.mWidth;
        this.materialLengthUnit = layout.mLength;
        this.quantity = layout.quantity;
    }

    calculateItemPixels() {
        this.marginXRPx = this.marginXRUnit * this.pixelsPerUnit;
        this.marginXLPx = this.marginXLUnit * this.pixelsPerUnit;
        this.marginYTPx = this.marginYTUnit * this.pixelsPerUnit;
        this.marginYBPx = this.marginYBUnit * this.pixelsPerUnit;

        this.itemLengthPx = this.itemLengthUnit * this.pixelsPerUnit + this.marginYTPx + this.marginYBPx;
        this.itemLengthPxNoMargie = this.itemLengthUnit * this.pixelsPerUnit;
        this.itemWidthPx = this.itemWidthUnit * this.pixelsPerUnit + this.marginXLPx + this.marginXRPx;
        this.itemWidthPxNoMargie = this.itemWidthUnit * this.pixelsPerUnit;
    }

    calculateMaterialPixels() {
        this.sideGuidesPx = this.sideGuidesUnit * this.pixelsPerUnit;
        this.gripperPx = this.gripperUnit * this.pixelsPerUnit;
        this.tailPx = this.tailUnit * this.pixelsPerUnit;

        this.materialWidthPx = this.materialWidthUnit * this.pixelsPerUnit;
        this.materialLengthPx = this.materialLengthUnit * this.pixelsPerUnit;
    }

    calculateDrawableArea() {
        this.drawableWidth = this.materialWidthPx - 2 * this.sideGuidesPx;
        this.drawableLength = this.materialLengthPx - (this.gripperPx + this.tailPx);
    }

    calculateMaxItems() {
        this.maxColCount = Math.floor(this.drawableWidth / this.itemWidthPx);
        this.maxRowCount = Math.floor(this.drawableLength / this.itemLengthPx);

        this.hardMaxItems = this.maxColCount * this.maxRowCount;
        this.softMaxItems = this.layout.up ? this.layout.up : this.hardMaxItems;

        this.calculatedSheetCount = Math.ceil(this.quantity / this.softMaxItems);
        this.lastSheetItemCount = this.calculatedSheetCount > 1 ? this.quantity % this.softMaxItems : 0;

        if (this.calculatedSheetCount > 1) {
            $('#fl_toggle_' + this.layout.materialId).css('visibility', 'visible');
        } else {
            $('#fl_toggle_' + this.layout.materialId).css('visibility', 'hidden');
        }

        $('#dbg_num_sheets_' + this.layout.materialId).html(this.calculatedSheetCount);
        $('#up_' + this.layout.materialId).val(this.softMaxItems).attr('data-max-up', this.hardMaxItems);

        if (this.layout.up > this.hardMaxItems && this.layout.up > this.quantity) {
            $('#custom_' + this.layout.materialId).hide();
            $(this.layout.materialIdSel).show();
        } else {
            this.CalcActualRowItemSum();
        }
    }

    calculateRotatedDimensions() {
        this.itemRotatedLengthPx = this.itemWidthPx;  // when rotated, length becomes width
        this.itemRotatedWidthPx = this.itemLengthPx;  // and width becomes length
    }

    CalcActualRowItemSum() {
        const regionsInRow = $(this.layout.materialIdSel + ' .row:first-child .item-region');
        let sum = 0;

        regionsInRow.each((index, element) => {
            const itemWidth = $(element).outerWidth();
            const itemMarginXR = parseFloat($(element).css('margin-right'));
            const itemMarginXL = parseFloat($(element).css('margin-left'));
            sum += itemWidth + itemMarginXR + itemMarginXL;
        });

        this.rowItemSum = sum;
    }

    CalcActualColumnItemSum() {
        const rows = $(this.layout.materialIdSel + ' .row');
        let sum = 0;

        rows.each((index, row) => {
            const firstItem = $(row).find('.item-region:first-child');
            const itemLength = firstItem.outerHeight();
            const itemMarginYB = parseFloat(firstItem.css('margin-bottom'));
            const itemMarginYT = parseFloat(firstItem.css('margin-top'));
            sum += itemLength + itemMarginYB + itemMarginYT;
        });

        this.colItemSum = sum;
    }
}

class LayoutManager {
    constructor(layout) {
        this.layout = layout;
        this.calcs = new LayoutCalculations(this.layout);

        if (this.layout.showLast) {
            this.calcs.quantity = this.calcs.lastSheetItemCount;
        }

        this.currentIndex = this.drawItems(this.calcs);
        this.handleLeftovers(this.calcs, this.currentIndex);
    }

    drawItems(calcs) {
        console.log('Drawing Items');
        $(calcs.layout.materialIdSel).css({
            'padding-top': calcs.gripperPx,
            'padding-bottom': calcs.tailPx,
            'padding-left': calcs.sideGuidesPx,
            'padding-right': calcs.sideGuidesPx
        });

        let itemTracker = 0;

        // Draw primary items
        for (let r = 0; r < calcs.maxRowCount; r++) {
            const $newRowStart = $('<div class="row d-inline-block p-0 m-0">');

            for (let c = 0; c < calcs.maxColCount; c++) {
                const $newItem = this.createItem(calcs, r, c);

                if (itemTracker < calcs.quantity) {
                    $newItem.addClass('item');
                }

                $newRowStart.append($newItem);
                itemTracker++;

                if (this.shouldBreak(calcs, itemTracker)) break;
            }

            $(calcs.layout.materialIdSel).append($newRowStart);

            if (this.shouldBreak(calcs, itemTracker)) break;
        }

        $(calcs.layout.materialIdSel + ' .row').css('height', calcs.itemLengthPxNoMargie);

        // Draw rotated items in the remaining space
        if (calcs.layout.up && calcs.layout.up > calcs.quantity) {
            let rotatedItemTracker = calcs.quantity;
            const remainingItems = calcs.hardMaxItems - calcs.quantity;

            for (let r = 0; r < calcs.maxRowCount && rotatedItemTracker < calcs.layout.up; r++) {
                const $newRowStart = $('<div class="row d-inline-block p-0 m-0">');

                for (let c = 0; c < calcs.maxColCount && rotatedItemTracker < calcs.layout.up; c++) {
                    const $newItem = this.createRotatedItem(calcs, r, c);

                    if (rotatedItemTracker < calcs.layout.up) {
                        $newItem.addClass('item');
                    }

                    $newRowStart.append($newItem);
                    rotatedItemTracker++;

                    if (rotatedItemTracker >= calcs.layout.up) break;
                }

                $(calcs.layout.materialIdSel).append($newRowStart);

                if (rotatedItemTracker >= calcs.layout.up) break;
            }
        }

        return itemTracker;
    }

    createItem(calcs, row, col) {
        const $newItem = $('<div>').addClass('item-region')
            .css({
                height: calcs.itemLengthPxNoMargie,
                width: calcs.itemWidthPxNoMargie
            });

        if (row !== 0) $newItem.css('margin-top', calcs.marginYTPx);
        if (col > 0) $newItem.css('margin-left', calcs.marginXLPx);
        if (row !== (calcs.maxRowCount - 1)) $newItem.css('margin-bottom', calcs.marginYBPx);
        if (col < (calcs.maxColCount - 1)) $newItem.css('margin-right', calcs.marginXRPx);

        return $newItem;
    }

    createRotatedItem(calcs, row, col) {
        const $newItem = $('<div>').addClass('item-region rotated')
            .css({
                height: calcs.itemRotatedLengthPx,
                width: calcs.itemRotatedWidthPx,
                opacity: 0.5 // optional: dim the rotated items
            });

        if (row !== 0) $newItem.css('margin-top', calcs.marginYTPx);
        if (col > 0) $newItem.css('margin-left', calcs.marginXLPx);
        if (row !== (calcs.maxRowCount - 1)) $newItem.css('margin-bottom', calcs.marginYBPx);
        if (col < (calcs.maxColCount - 1)) $newItem.css('margin-right', calcs.marginXRPx);

        return $newItem;
    }

    shouldBreak(calcs, itemTracker) {
        return calcs.isMixGrain ? (itemTracker === calcs.quantity || itemTracker === calcs.hardMaxItems)
            : (itemTracker === calcs.quantity || itemTracker === calcs.softMaxItems);
    }

    handleLeftovers(calcs, currentIndex) {
        const useLeftovers = calcs.isMixGrain;

        if (useLeftovers) {
            // Handle mix grain specific logic here
        }

        while (currentIndex < calcs.hardMaxItems && currentIndex < calcs.quantity) {
            const $newItem = $('<div>')
                .addClass('item-region')
                .css({
                    height: calcs.itemLengthPxNoMargie,
                    width: calcs.itemWidthPxNoMargie
                });

            $('.mix-row').eq(currentIndex % calcs.leftoverColumns).append($newItem);
            currentIndex++;
        }
    }

    createItemMeasurementDisplay(width, length) {
        var display = document.createElement('div');

        display.style.position = 'relative';
        display.style.width = width + 'px';
        display.style.height = length + 'px';
        display.innerHTML = `<span style="position: absolute; bottom: 0; left: 0; right: 0; text-align: start; font-size: 16px; color: black;">
                              ${width / 10}"</span>
                             <span style="position: absolute; right: 0; bottom: 0; top: 2px; font-size: 16px; color: black; writing-mode: vertical-rl; text-align: end; text-orientation: sideways; transform: rotate(180deg);">
                              ${length / 10}"</span>`;
        return display;
    }

    drawLayoutLines(calcs) {
        let layoutDisplay = document.querySelector('.layout-display');
        let material = document.getElementById(calcs.layout.materialId);
        let itemRegion = material.getElementsByClassName('item-region')[0];
        itemRegion.appendChild(this.createItemMeasurementDisplay(itemRegion.offsetWidth, itemRegion.offsetHeight));

        // material.appendChild(this.createLegendLine(material.offsetWidth, material.offsetHeight));
    }
}