//datatable vars (not currently used?)
var qst, mst, pst;

//hotfix/hack for resetting collapses
//easier to iterate all collapse-parent items and click them then to determine which are open and iterate those (due to cur implementation)
//so, if this var is set to true and ExpandCollapseList is called, the list will only be collapsed (hidden). It will not be expanded (shown).
//note: the expand/collapse is actually d-none functionality and bs collapse
var resetting = false;

//hotfix/hack for opening necessary collapsed
//when a select service button is clicked, we need to select all currently selected services (for user convenience)
//we also need to expand the collapsed lists, so the user can see the checked boxes.
//if configging is true and ExpandCollapseList is called, only lists with checked boxes will be shown.
var configging = false;

//hotifx/hack for view layout functionality
//if this is true, redirect to the layout edit view after saving the quote
//if this is false, redirect to the service arrangement view after saving the quote
var viewingLayout = false;

//hotfix/hack for saving record
//if true, just save and nothing else
var justSaving = false;

var tempNotes = new Array();
var $activeServiceItem;

var doingCalcs = false;

let itemWidthLengthCur = {
    itemId: 0,
    length: 0,
    width: 9
};

function ConvertFormattedToDecimal() {
    //get the formatted time
    //ajax request for decimal time

    var fTime = $('input[name="ServiceTimeEditor.FormattedServiceTime"]').val();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('formatted-time-to-decimal'),
        data: {
            'formattedTime': fTime
        },
        success: function (data) {
            try {
                //res is not used; just to see if the data is valid JSON
                let res = JSON.parse(data);
                $('input[name="ServiceTimeEditor.ServiceTime"]').val(data);
            } catch (e) {
                $('input[name="ServiceTimeEditor.ServiceTime"]').val('');
            }
        }
    });
};
function ConvertServiceTimeToFormatted() {
    //get the service time
    //ajax request for formatted time
    //set the formatted time
    var dTime = $('input[name="ServiceTimeEditor.ServiceTime"]').val();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('decimal-time-to-formatted'),
        data: {
            'decimalTime': dTime
        },
        success: function (data) {
            $('input[name="ServiceTimeEditor.FormattedServiceTime"]').val(data);
        }
    });
};

function LoadMaterials() {
    let $cat = $(this);
    let rId = $cat.attr('data-record-id');

    $.ajax({
        type: 'GET',
        url: ProcessUrl('load-materials-by-category' + '/' + rId),
        success: function (data) {
            try {
                let res = JSON.parse(data);

                // Assuming the table has an id of 'materialsTable'
                let $table = $('#materials_select_table');

                // Destroy any existing DataTable to reinitialize it later
                if ($.fn.DataTable.isDataTable($table)) {
                    $table.DataTable().destroy();
                }

                // Empty the table body
                $table.find('tbody').empty();

                // Iterate through the result and insert rows
                for (let i = 0; i < res.length; i++) {
                    let rowHtml =
                        `
                            <tr class="admin-edit-item-material"
                                data-material-size="${res[i].Size}"
                                data-item-title="${res[i].Title}"
                                data-record-id="${res[i].Id}"
                                data-mat-cost-val="${res[i].Cost}"
                                data-mat-cost-formatted="${res[i].CostFormatted}"
                                data-mat-cost-unit="${res[i].CostType}">
                                <td>
                                    <a href="javascript:void(0);" class="material-title">${res[i].Title}</a>
                                </td>
                                <td>

                                </td>
                                <td>
                                    ${res[i].Size}
                                </td>
                                <td>
                                    ${res[i].Stock}
                                </td>
                                <td>
                                    ${res[i].CostFormatted} ${res[i].CostType}
                                </td>
                            </tr>
                        `;
                    $table.find('tbody').append(rowHtml);
                }
                // Reinitialize DataTable
                $table.DataTable();

                //scroll to the top of the view
                $('#materials_select_modal .nu-viewport').scrollTop(0);
            } catch (e) {
                console.error('Error processing materials data:', e);
            }
        }
    })
};

function AddButtonHandler() {
    var checkedItems = $('#materials_select input[type="checkbox"]:checked').length;

    if (checkedItems > 0) {
        $('.ps-add-btn').prop('disabled', false);
    } else {
        $('.ps-add-btn').prop('disabled', true);
    }
};

function AddItem() {
    var masterQuoteId = $('#VM_Quote_Quote_Id').val();

    $.get((ProcessUrl('add-item/quote/') + masterQuoteId), function (data) {
        $('#job_items').append(JSON.parse(data));
        NonTableRowRemoveAddHandler();
    });
};

function AddOutsourcedItem() {
    var masterQuoteId = $('#VM_Quote_Quote_Id').val();

    $.get((ProcessUrl('add-outsourced-item/quote/') + masterQuoteId), function (data) {
        $('#job_items').append(JSON.parse(data));
    });
};

function RemoveItem() {
    var $btn = $(this);
    var $row = $btn.parents('.item-row');
    var rowId = $row.attr('data-record-id');
    var isOutsource = $btn.hasClass('remove-os-btn');

    $.post((ProcessUrl('remove-item/quote/') + rowId + '/' + isOutsource), function () {
        $row.remove();
        NonTableRowRemoveAddHandler();
    });
};

function CreateServiceSelect(prodId) {
    //creates an id like service_select_{prodId}_{# instance of this product}
    var newId = ('service_select_' + prodId + '_' + ($('div[id^="service_select_' + prodId + '"]').length + 1)).toString();

    //create a new div with id newId inside of #service_select_forms
    $('#service_select_form').append('<div id="' + newId + '" class="service-select-form"></div>');

    $.ajax({
        type: 'GET',
        url: ProcessUrl('Services/Get/All'),
        success: function (data) {
            $('#' + newId).html(data);
        }
    });
};

//these should be events on modal shown/hide
function SelectServices() {
    var sid = $(this).parents('.item-row').attr('data-record-id');
    $('#sp_adder_helper').val(sid);
};

function SelectMaterials() {
    var pid = $(this).parents('.quote-item-row').attr('data-record-id');
    $('#sp_adder_helper').val(pid);
    setTimeout(function () {
        $('#materials_select_modal .nu-viewport .sidebar-container ul li[data-record-id="-1"]').trigger('click');
    }, 500);
};

function SelectPresses() {
    var pid = $(this).parents('.quote-item-row').attr('data-record-id');
    $('#sp_adder_helper').val(pid);
};

function CustomFormSerialization(evt) {
    evt.preventDefault();
    $('.client-autocomplete').prop('disabled', true);
    //declare a new array for the objects (serializing the items)
    var arr = [];
    var oArr = [];
    var index = 1;

    //Validator.js
    CheckFormValidation();

    //failedValidation is declared in Validator.js
    if (!failedValidation) {
        $('.o-item-row').each(function () {
            var $oRow = $(this);
            var obj = {};

            obj['Id'] = $oRow.attr('data-record-id');
            obj['Quote'] = $oRow.find('.quote-id').val();
            obj['Description'] = $oRow.find('.item-description').val();
            obj['Notes'] = $oRow.find('.item-notes').val();
            obj['ShippingCost'] = $oRow.find('.shipping-cost').val();
            obj['JobCost'] = $oRow.find('.job-cost').val();
            obj['Vendor'] = $oRow.find('.vendor').val();
            obj['Quantity'] = $oRow.find('.o-item-qty').val();
            obj['JobPriceUnit'] = $oRow.find('.job-price-unit').val();

            oArr.push(obj);
        });
        $('.quote-item-row').each(function () {
            var $row = $(this);
            var obj = {};
            let speedUnit = $row.find('.selected-press-speed-display .speed-unit').html().trim();
            $('#print_speed_ref_sl option').each(function () {
                var $option = $(this);
                if ($option.text().trim() == speedUnit) {
                    $option.prop('selected', true);
                }
            });
            obj['Id'] = $row.attr('data-record-id');
            //set the Title property of obj to the value contained in QuoteItems.Title
            obj['Title'] = $row.find('.item-title').val();
            obj['Quantity'] = $row.find('.item-quantity').val();
            obj['Width'] = $row.find('.item-width').val();
            obj['Length'] = $row.find('.item-length').val();
            obj['Quote'] = $('input[name="VM_Quote.Quote.Id"]').val();
            obj['Material'] = $row.find('.selectedmaterial').val();
            obj['Description'] = $row.find('.item-description').val();
            obj['Notes'] = $row.find('.item-notes').val();
            obj['MaterialRunSize'] = $row.find('.current-mrs').val();
            obj['PressSpeedDenominator'] = $('#print_speed_ref_sl option:selected').val();
            obj['PressSpeedNumerator'] = $row.find('.selected-press-speed-display .speed').html().trim();
            obj['Press'] = $row.find('.selectedpress').val();
            obj['Up'] = $row.find('.calced-up-val').html().trim();
            obj['QuantityBy'] = $row.find('.item-quantity-by').val();
            obj['TotalQuantity'] = (obj['Quantity'] * obj['QuantityBy']);
            //I don't think this vvv needs to be here because it's all taken care of "behind the scenes"
            //obj['UserSheetOverride'] = $row.find('.user-sheet-override').val();
            obj['InternalNotes'] = $row.find('.internal-notes').val();
            obj['UpValueLocked'] = $row.find('.up-value-locked').is(':checked');
            obj['SelectedSpeed'] = $row.find('.selected-speed').val();
            obj['MaterialNotes'] = $row.find('.chosen-material-notes .material-notes').val();
            obj['DesignHours'] = $row.find('.design-hours').val();
            obj['ArtworkProvided'] = !$row.find('#is_design_required').is(':checked'); //indicate the opposite of the checkbox state for the model

            // Create an empty array variable to store the combined list items
            var combinedList = [];

            $row.find('.service-list li').each(function () {
                combinedList.push($(this));
            });

            //create a new array for the services
            var services = [];

            for (var i = 0; i < combinedList.length; i++) {
                let serObj = {};
                serObj['OriginalService'] = combinedList[i].attr('data-record-id');
                serObj['Id'] = combinedList[i].attr('data-record-id');
                serObj['QuoteItemId'] = $row.attr('data-record-id');
                serObj['Ordinal'] = (i + 1);
                serObj['IsCuttingService'] = $(combinedList[i]).find('.is-cutting-service').val();
                services.push(serObj);
            }

            obj['QuoteItemServices'] = services;

            arr.push(obj);
        });

        var form = $('#quote_builder').serializeArray();
        let formParsed = {};
        $.each(form, function (index, field) {
            formParsed[field.name.replace('VM_Quote.Quote.', '')] = field.value;
        });
        formParsed['MarkupOverride'] = Number($('.markup-input').val()) / 100.0000;
        formParsed['IsPickup'] = $('#rad_pickup').is(':checked');
        formParsed['ShippingMethod'] = $('#VM_Quote_Quote_ShippingMethod').val();
        formParsed['IsWarehouseJob'] = $('#VM_Quote_Quote_IsWarehouseJob_Value').is(':checked');
        formParsed['IsGolfEvent'] = $('#VM_Quote_Quote_IsGolfEvent_Value').is(':checked');

        $.ajax({
            type: 'POST',
            url: ProcessUrl('Quote/Update'),
            data: {
                q: formParsed,
                items: arr,
                outsourcedItems: oArr,
                tempNotes: tempNotes
            },
            complete: function (data) {
                try {
                    let res = JSON.parse(data.responseJSON);

                    for (var i = 0; i < res.length; i++) {
                        let curId = res[i].Id;
                        let curUp = res[i].Up;
                        let $curRow = $('.quote-item-row[data-record-id="' + curId + '"]');
                        //update the up value display as well as the hidden field
                        $curRow.find('.calced-up-val').html(curUp);
                        $curRow.find('.item-up-val').val(curUp);
                    }

                    if (doingCalcs) {
                        doingCalcs = false;
                        PerformCalcs();
                    }
                } catch (e) { /*//console.log(e);*/ }
            }
        });
    } else {
        alert('Please complete the necessary fields');
    }
};

function AddPrepressServicesToItem(evt) {
    var $tgt = $(evt.target);

    var itemId = $('#sp_adder_helper').val();
    var list = $('#prepress_service_select_table tr');
    var pressDefaultHelper = $('#press_add_helper').val();
    var listDestination = '.quote-item-row[data-record-id="' + itemId + '"] .prepress-service-list';

    var arr = new Array();

    for (var i = 0; i < list.length; i++) {
        var tArr = $(list[i]).find('input[type="checkbox"][data-record-type="service"]:checked').map(function () {
            return {
                ServiceId: $(this).attr('data-service-id'),
                ServiceTitle: $(this).attr('data-item-title'),
                RecordId: 0,
                IsCuttingService: false
            };
        }).get();

        arr = [...arr, ...tArr];
    }

    var mod = { ItemServiceDetails: arr, RowId: itemId, RecordType: "quote" };

    $.ajax({
        type: 'POST',
        url: ProcessUrl('item-service-update'),
        data: {
            items: mod,
            pressId: pressDefaultHelper,
            serviceType: 'Prepress'
        },
        success: function (data) {
            try {
                var res = JSON.parse(data);

                $('#prepress_service_select_table input[type="checkbox"]:checked').prop('checked', false);

                $(listDestination).html('');
                $(listDestination).html(res.services);
            } catch (e) { }
        }
    });
};

function AddCuttingServicesToItem(evt) {
    var $tgt = $(evt.target);

    var itemId = $('#sp_adder_helper').val();
    var list = $('#cutting_service_select_table tr');
    var pressDefaultHelper = $('#press_add_helper').val();
    var listDestination = '.quote-item-row[data-record-id="' + itemId + '"] .cutting-service-list';

    var arr = new Array();

    for (var i = 0; i < list.length; i++) {
        var tArr = $(list[i]).find('input[type="checkbox"][data-record-type="service"]:checked').map(function () {
            return {
                ServiceId: $(this).attr('data-service-id'),
                ServiceTitle: $(this).attr('data-item-title'),
                RecordId: 0,
                IsCuttingService: true
            };
        }).get();

        arr = [...arr, ...tArr];
    }

    var mod = { ItemServiceDetails: arr, RowId: itemId, RecordType: "quote" };

    $.ajax({
        type: 'POST',
        url: ProcessUrl('item-service-update'),
        data: {
            items: mod,
            pressId: pressDefaultHelper,
            serviceType: 'Cutting'
        },
        success: function (data) {
            try {
                var res = JSON.parse(data);
                //console.log(res);
                $('#cutting_service_select_table input[type="checkbox"]:checked').prop('checked', false);

                $(listDestination).html('');
                $(listDestination).html(res.services);
            } catch (e) { }
        }
    });
};

function AddFinishingServicesToItem(evt) {
    var itemId = $('#sp_adder_helper').val();
    var list = $('#finishing_service_select_table tr');
    var pressDefaultHelper = $('#press_add_helper').val();
    var listDestination = '.quote-item-row[data-record-id="' + itemId + '"] .finishing-service-list';

    var arr = new Array();

    for (var i = 0; i < list.length; i++) {
        var tArr = $(list[i]).find('input[type="checkbox"][data-record-type="service"]:checked').map(function () {
            return {
                ServiceId: $(this).attr('data-service-id'),
                ServiceTitle: $(this).attr('data-item-title'),
                RecordId: 0,
                IsCuttingService: false
            };
        }).get();

        arr = [...arr, ...tArr];
    }

    var mod = { ItemServiceDetails: arr, RowId: itemId, RecordType: "quote" };

    $.ajax({
        type: 'POST',
        url: ProcessUrl('item-service-update'),
        data: {
            items: mod,
            pressId: pressDefaultHelper,
            serviceType: 'Finishing'
        },
        success: function (data) {
            try {
                var res = JSON.parse(data);
                //console.log(res);
                $('#finishing_service_select_table input[type="checkbox"]:checked').prop('checked', false);

                $(listDestination).html('');
                $(listDestination).html(res.services);
            } catch (e) {
            }
        }
    });
};

function AddMaterialToItem() {
    var $materialRow = $('#materials_select_table').find('.admin-edit-item-material.selected');
    let spAdderVal = $('#sp_adder_helper').val();
    var theSel = ('#sm_' + spAdderVal);
    let $theRow = $('.quote-item-row[data-record-id="' + spAdderVal + '"]');

    if ($materialRow.length === 0) {
        $(theSel).val('');
        $theRow.find('.selectedmaterial').val('');
        $theRow.find('.view-layout-btn').prop('disabled', true);
        $theRow.find('.calced-up-val').html('N/A');
        $theRow.find('#QuoteItem_Up').val('');
        $theRow.find('.total-sheets').html('N/A');
        $theRow.find('.total-run-sheets').html('N/A');
        return; // No material selected, do nothing
    } else {
        $theRow.find('.view-layout-btn').prop('disabled', false);
    }

    var itemTitle = $materialRow.find('.material-title').text().trim();

    var materialId = $materialRow.attr('data-record-id');
    var matSize = $materialRow.attr('data-material-size');
    var matCost = $materialRow.attr('data-mat-cost-formatted');
    var matCostUnit = $materialRow.attr('data-mat-cost-unit');
    var matSizeArr = matSize.split('x');
    var matLength = matSizeArr[0];
    var matWidth = matSizeArr[1];
    var pressId;
    var ppf, ppb;

    $.ajax({
        type: 'POST',
        url: ProcessUrl('reset-material-values-on-item'),
        data: {
            'id': $('#sp_adder_helper').val(),
            'itemType': 'quote',
            'materialId': materialId
        },
        success: function (data) {
            try {
                var res = JSON.parse(data);
                matCost = res.MaterialCostFormatted;
                matCostUnit = res.MatCostUnit;
                pressId = res.PressId;
                ppf = res.ColorFrontProfile;
                ppb = res.ColorBackProfile;

                //clear the Up Locked value for this item
                $theRow.find('input[name="QuoteItem.UpValueLocked.Value"]').prop('checked', false);
            } catch (e) { }
        },
        complete: function () {
            let $theRow = $('.quote-item-row[data-record-id="' + spAdderVal + '"]');
            //set run size values
            $theRow.find('.material-run-size-w').val(matWidth);
            $theRow.find('.material-run-size-l').val(matLength);
            //set the run size display
            $theRow.find('.run-size').html(matLength + ' x ' + matWidth);
            //set the material name display
            $theRow.find('.material-name').html(itemTitle);
            $theRow.find('.sheets-display').html('-');
            $theRow.find('.scrap-sheets-display').html('-');
            $theRow.find('.run-sheets-display').html('-');
            //show the cost and unit
            $theRow.find('.selected-material-cost').html(matCost);
            $theRow.find('.selected-material-cost-unit').html(matCostUnit);

            $theRow.find('.front-color-display').html(ppf);
            $theRow.find('.back-color-display').html(ppb);

            var $selectedPressInput = $('.quote-item-row[data-record-id="' + spAdderVal + '"] .selectedpress');
            $selectedPressInput.val(pressId);

            $(theSel).val(materialId);

            $theRow.find('.up-up-btn').prop('disabled', false);
            $theRow.find('.up-down-btn').prop('disabled', false);

            //set material run size
            $theRow.find('.current-mrs').val(matLength + 'x' + matWidth);
        }
    });
};

function AddPressToItem() {
    var $pressRow = $('#press_select_table').find('tr.selected');
    var spAdderVal = $('#sp_adder_helper').val();
    var theSel = $('#sp_' + spAdderVal);

    if ($pressRow.length === 0) {
        $(theSel).val('');
        return;
    }

    var pressId = $pressRow.attr('data-press-id');
    var pressTitle = $pressRow.find('.press-title').text().trim();
    var $quoteItemRow = $('.quote-item-row[data-record-id="' + spAdderVal + '"]');

    // Update UI elements
    $('#press_add_helper').val(pressId);
    $quoteItemRow.find('.press-name').html(pressTitle);
    $quoteItemRow.find('.selectedpress').val(pressId);
    $(theSel).val(pressId);

    let selectedSpeed = $quoteItemRow.find('.selected-speed').val();

    // Primary AJAX request: Add press to item
    $.post(ProcessUrl('add-press-to-item'), {
        itemType: 'QUOTE',
        itemId: spAdderVal,
        pressId: pressId,
        speedId: selectedSpeed
    }).done(function (response) {
        try {
            let res = JSON.parse(response);
            $quoteItemRow.find('.selectedpress').val(res.pressId);
            //let speedId = $quoteItemRow.find('.selected-speed').val();

            //update the selected speed input
            $quoteItemRow.find('.selected-speed').val(res.speedId);

            //update the display values
            $quoteItemRow.find('.selected-press-speed-display .speed').html(res.pressSpeed);
            $quoteItemRow.find('.selected-press-speed-display .speed-unit').html(res.pressSpeedUnit);

            //ajax request for default finishing and prepress services
            $.ajax({
                type: 'GET',
                url: ProcessUrl('Press/GetDefaultServices/') + res.pressId + '/' + spAdderVal + '/' + res.speedId + '/quote',
                success: function (data) {
                    var d = JSON.parse(data);

                    for (let i = 0; i < d.prepress.length; i++) {
                        let qs = '#prepress_service_select_table input[type="checkbox"][data-record-type="service"][data-service-id="' + d.prepress[i].Service + '"]';
                        $(qs).prop('checked', true);
                    }
                    for (let i = 0; i < d.cutting.length; i++) {
                        let qs = '#cutting_service_select_table input[type="checkbox"][data-record-type="service"][data-service-id="' + d.cutting[i].Service + '"]';
                        $(qs).prop('checked', true);
                    }
                    for (let i = 0; i < d.finishing.length; i++) {
                        let qs = '#finishing_service_select_table input[type="checkbox"][data-record-type="service"][data-service-id="' + d.finishing[i].Service + '"]';
                        $(qs).prop('checked', true);
                    }

                    $('.builder-prepress-service-add').trigger('click');
                    $('.builder-cutting-service-add').trigger('click');
                    $('.builder-finishing-service-add').trigger('click');

                    $('#press_select_modal').modal('hide');

                    setTimeout(function () {
                        $('#press_add_helper').val('');
                    }, 1000);
                }
            });
        } catch (error) {
            console.error("Error parsing response:", error);
        }
    }).fail(handleAjaxError);
};

// Helper function to update service checkboxes
function updateServiceSelections(data) {
    let serviceTables = {
        prepress: '#prepress_service_select_table',
        cutting: '#cutting_service_select_table',
        finishing: '#finishing_service_select_table'
    };

    $.each(serviceTables, function (key, tableSelector) {
        if (data[key]) {
            data[key].forEach(service => {
                $(`${tableSelector} input[type="checkbox"][data-record-type="service"][data-service-id="${service.Service}"]`).prop('checked', true);
            });
        }
    });
};

// Helper function to trigger service addition buttons
function triggerServiceAdditions() {
    $('.builder-prepress-service-add, .builder-cutting-service-add, .builder-finishing-service-add').trigger('click');
};

// Centralized error handler
function handleAjaxError(jqXHR, textStatus, errorThrown) {
    console.error("AJAX Request Failed:", textStatus, errorThrown, jqXHR.responseText);
};

function CheckJobNumber() {
    $.get(ProcessUrl('check/' + $('#VM_Quote_Quote_JobNumber').val() + '/' + $('#VM_Quote_Quote_Id').val() + '/Quote'), function (data) {
        let isValid = (data === false);

        if (isValid) {
            //enable create button
            $('input[type="submit"]').prop('disabled', false);
            //add job-num-valid class to #Quote_JobNumber
            $('#VM_Quote_Quote_JobNumber').removeClass('job-num-invalid').addClass('job-num-valid');
        } else {
            //disable create button
            $('input[type="submit"]').prop('disabled', true);
            //add job-num-invalid class to #Quote_JobNumber
            $('#VM_Quote_Quote_JobNumber').removeClass('job-num-valid').addClass('job-num-invalid');
        }
    });
};

function ExpandCollapseList() {
    var theKey = $(this).attr('data-collapser-key');
    var $list = $('.collapser-child[data-collapser-key="' + theKey + '"]');

    //if any $list items have d-none, show the $list. Otherwise hide it
    //HOTFIX/HACK FROM ABOVE: check "resetting" var and handle appropriately
    //HOTFIX/HACK FROM ABOVE: check "configging" var and handle appropriately
    if ($list.hasClass('d-none') && !resetting) {
        if (configging) {
            let anyChecked = false;  // Flag to indicate if any checkbox is checked

            $list.each(function () {
                const checkbox = $(this).find('input[type="checkbox"]');

                if (checkbox.is(':checked')) {
                    anyChecked = true;
                    return false;  // Exit the loop if any checkbox is checked
                }
            });

            if (anyChecked) {
                $list.removeClass('d-none');
            }
        } else {
            $list.removeClass('d-none');
        }
    } else {
        $list.addClass('d-none');
    }
};

function SelectMaterialRow() {
    var $clicked = $(this);
    if ($clicked.hasClass('selected')) {
        $clicked.removeClass('selected');
    } else {
        $('#materials_select_table tbody tr.selected').removeClass('selected');
        $clicked.addClass('selected');
    }
};

function SelectPressRow() {
    var $clicked = $(this).parents('tr');
    if ($clicked.hasClass('selected')) {
        $clicked.removeClass('selected');
    } else {
        $('#press_select_table tbody tr.selected').removeClass('selected');
        $clicked.addClass('selected');
    }
};

function ResetCollapsers() {
    resetting = true;

    $('.collapser-parent').each(function () {
        $(this).trigger('click');
    });

    resetting = false;
};

function FrontColorInputHandler() {
    var $inp = $(this);
    var entry = $inp.val().toUpperCase();

    var $ctInp = $inp.parents('.chosen-press-colors-front').find('.press-color-count-front');
    var $nInp = $inp.parents('.chosen-press-colors-front').find('.press-color-notes-front');

    if (entry !== '4CP') {
        $ctInp.show();
        $nInp.show();
    } else {
        $ctInp.hide();
        $nInp.hide();
    }
};

function BackColorInputHandler() {
    var $inp = $(this);
    var entry = $inp.val().toUpperCase();

    var $ctInp = $inp.parents('.chosen-press-colors-back').find('.press-color-count-back');
    var $nInp = $inp.parents('.chosen-press-colors-back').find('.press-color-notes-back');

    if (entry !== 'NONE') {
        $ctInp.show();
        $nInp.show();
    } else {
        $ctInp.hide();
        $nInp.hide();
    }
};

function LoadClientContacts() {
    $('#contact_autocomplete').prop('disabled', false);
};

function ShowHideContacts() {
    if ($('.contact-list-row').is(':visible')) {
        $('.contact-list-row').hide();
        $('.show-hide-contacts').text('Show Contacts');
    } else {
        $('.contact-list-row').show();
        $('.show-hide-contacts').text('Hide Contacts');
    }
};

function ViewLayout() {
    let rId = $(this).parents('.quote-item-row').attr('data-record-id');
    $('#sp_adder_helper').val(rId);

    $.ajax({
        type: 'GET',
        url: ProcessUrl('view-quote-layout2') + '/' + rId,
        success: function (data) {
            try {
                let res = JSON.parse(data);
                $('#layout_modal .modal-body').html(res.html);
                $('#layout_modal').modal('show');
            } catch (e) {
            }
        }
    })
};

function SpeedRadioHandler() {
    var $radio = $(this);
    var $p = $radio.parents('tr');

    var pSpeed = $p.attr('data-speed-val');
    var pSpeedUnit = $p.attr('data-speed-unit-val');

    $('.custom-speed-entry .override-speed-numerator').val(pSpeed);
    $('.custom-speed-entry #SpeedDenominator').val(pSpeedUnit);

    if ($p.hasClass('custom-speed')) {
        $('.custom-speed-entry').show();
    } else {
        $('.custom-speed-entry').hide();
    }
};

function UpdateItemSpeedInfo() {
    //if the $row of the selected radio has class "custom-speed" then get the value from the manual entry
    //otherwise do below
    var currentItemId = $('.current-item-id').val();
    var $p = $('#press_speed_select_table input[type="radio"]:checked').parents('tr');
    var pressSpeedId = $p.attr('data-record-id');
    var $row = $('.item-row[data-record-id="' + currentItemId + '"]');

    var pSpeed = $p.attr('data-speed-val');
    var pSpeedUnit = $p.attr('data-speed-unit-val');
    var pSpeedText = $p.attr('data-speed-unit-text');

    if ($p.hasClass('custom-speed')) {
        //get the custom speed
        pSpeed = $('#press_speed_select_modal .custom-speed-entry .override-speed-numerator').val();
        pSpeedUnit = $p.find('#press_speed_select_modal .custom-speed-entry #SpeedDenominator').val();
        pSpeedText = $p.find('#press_speed_select_modal .custom-speed-entry #SpeedDenominator option:selected').text();
    }

    $row.find('.selected-speed').val(pressSpeedId);

    $row.find('.selected-press-speed-display .speed').html(pSpeed);
    $row.find('.selected-press-speed-display .speed-unit').html(pSpeedText);
};

function LoadItemDocuments() {
    var $ele = $(this);
    var itemId = $ele.parents('.item-row').attr('data-record-id');
    var isOutsourced = $ele.parents('.item-row').hasClass('o-item-row');
    //set the field in the add document modal indicating if outsourced or not as a jic
    $('.ad-is-outsourced-item').val(isOutsourced);
    $('.delete-docs-btn').hide();

    $.ajax({
        type: 'GET',
        url: ProcessUrl('GetDocuments/quote/' + itemId + '/' + isOutsourced),
        beforeSend: function () {
            $('.document-list').html('');
        },
        success: function (data) {
            var res = JSON.parse(data);

            for (var i = 0; i < res.length; i++) {
                $('.document-list').append('<li data-doc-id="' + res[i].QuoteDocument.Id + '"><input type="checkbox" class="del-marker-cb" />' + res[i].DocumentName + '<i class="fa-regular fa-download"></i></li>');
            }

            $('.item-id').val(itemId);
        },
        complete: function () {
            $('#document_list_modal').modal('show');
        }
    });
};

function AddDocument() {
    //hide the document list modal and show the add document modal
    $('#document_list_modal').modal('hide');
    $('.item-type').val('quote');
    $('#add_document_modal').modal('show');
};

function AddDocCancel() {
    $('#document_list_modal').modal('show');
};

function ShowDeleteModal() {
    $('#document_list_modal').modal('hide');
    $('#delete_document_modal').modal('show');
};
function DeleteFromThisItem() {
};
function DeleteFromAllItems() {
};

function DownloadFile() {
    var $dlBtn = $(this);
    var id = $dlBtn.parent().attr('data-doc-id');

    window.open(ProcessUrl('get-document/' + id + '/quote'), "_blank");
};
function AdjustMaterialCost() {
    var $btn = $(this);
    var $row = $btn.parents('.item-row');
    $('#current_record_id').val($row.attr('data-record-id'));
    var curCost = $btn.parent().find('.material-cost-fmt').html().trim();
    $('#mat_price_override_modal .modal-body .current-material-price').html(curCost);
};

function SubmitMaterialPriceOverride() {
    var newPrice = $('#mat_price_override_modal .modal-body .new-material-price').val();
    var recordId = $('#mat_price_override_modal .modal-body #current_record_id').val();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('mat-price-override'),
        data: {
            id: recordId,
            newPrice: newPrice,
            recordType: 'Quote'
        },
        complete: function () {
            $('.quote-item-row[data-record-id="' + recordId + '"]').find('.material-cost-fmt').html('$' + newPrice);
        }
    });
};

function UpdateContact() {
    try {
        var contactId = $(this).parents('li').attr('data-record-id');
        $('#VM_Quote_Quote_Contact').val(contactId);
    } catch (e) { }
};

function SaveRecord() {
    justSaving = true;
    $('form#quote_builder').submit();
};

function CopyItem() {
    //CopyItem currently does NOT apply to outsourced items (just make another one manually)
    var $btn = $(this);
    var recordId = $btn.parents('.quote-item-row').attr('data-record-id');

    $.post(ProcessUrl('copy-item') + '/' + recordId + '/quote', function (data) {
        try {
            var res = JSON.parse(data);

            $('#job_items').append(res);

            NonTableRowRemoveAddHandler();
        } catch (e) { }
    });
};
function FocusCostField() {
    $('.new-material-price').focus();
};

function ViewQuoteTicket() {
    //Validator.js
    CheckFormValidation();

    if (failedValidation === false) {
        doingCalcs = true;

        //submit the form
        let formId = '#job_builder';
        let $theForm = $(formId);

        if (!$theForm.length) {
            $theForm = $('#quote_builder');
        }

        $theForm.submit();

        //show a modal with a checkbox: show Total?
        //call endpoint with 1 or 0 indicating to show total or not

        /*setTimeout(function () {
            window.location = ProcessUrl('GetQuoteTicket/' + $('#VM_Quote_Quote_Id').val());
        }, 2000);*/
    }
};

function SetHelperToRowId() {
    var rId = $(this).parents('.quote-item-row').attr('data-record-id');
    $('#sp_adder_helper').val(rId);
};

function OverrideSheetCount() {
    $('#sp_adder_helper').val($(this).parents('.item-row').attr('data-record-id'));

    //open the modal
    $('#sheet_override_modal').modal('show');
};

function DoneEditingFinishingConfig() {
    var arr = [];
    //foreach tr in #finishing_material_config_table tbody
    $('#finishing_material_config_table tbody tr').each(function () {
        let $tr = $(this);
        let recordId = $tr.attr('data-record-id');
        let newConfigValue = $tr.find('.f-mat-config-val').val();
        let newAppValue = $tr.find('select').val();
        arr.push({ id: recordId, newValue: newConfigValue, newApplicationValue: newAppValue });
    });

    //get data-record-id from tr
    //get config value from .f-mat-config-val input inside of tr's td
    //POST an array of id/input values to /update-finishing-config along with the itemType
    $.ajax({
        type: 'POST',
        url: ProcessUrl('update-finishing-config'),
        data: {
            configs: arr,
            itemtype: 'quote'
        }
    });
};

function ClearMaterialConfigTableBody() {
    $('#finishing_material_config_table tbody tr').html('');
};

function RemoveServiceEntry() {
    let qisPrintServiceId = $(this).parents('li').attr('data-record-id'); // this is the quote item service id that needs to be deleted
    $.post(ProcessUrl('ajax-remove-service-entry') + '/' + qisPrintServiceId + '/quote');
};
function PickupShipToggle() {
    if ($('#rad_ship').is(':checked')) {
        //show shipping dropdown
        $('.ship-select-row').show();
    } else {
        //hide shipping dropdown
        $('.ship-select-row').hide();
    }
};

function UpdateShippingAddress() {
    let radio = $('#shipping_address_modal input:checked');
    let address = radio.parents('li').attr('data-record-id');
    //let selectedRadio = $('#shipping_address_modal input[type="radio"]:checked').parents('li').prop('data-record-id');
    let quoteId = $('#VM_Quote_Quote_Id').val();

    $('#VM_Quote_Quote_ShippingAddress').val(address);

    $.ajax({
        type: 'POST',
        url: ProcessUrl('shipping-address-change'),
        data: {
            itemtype: 'quote',
            addressId: address,
            recordId: quoteId
        },
        success: function (data) {
            //update shipping address display
            let $display = $('.sa-display-wrapper');

            $display.find('.sa-customer-name').html(data.CustomerName);
            $display.find('.sa-customer-address').html(data.CustomerAddress);
            $display.find('.sa-customer-csz').html(data.CustomerCSZ);
        }
    });
};

function UpdateFreightShippingAddress() {
    let radio = $('#freight_shipping_address_modal input:checked');
    let address = radio.parents('li').attr('data-record-id');
    //let selectedRadio = $('#shipping_address_modal input[type="radio"]:checked').parents('li').prop('data-record-id');
    let quoteId = $('#VM_Quote_Quote_Id').val();

    $('#VM_Quote_Quote_FreightAddress').val(address);

    $.ajax({
        type: 'POST',
        url: ProcessUrl('freight-address-change'),
        data: {
            itemtype: 'quote',
            addressId: address,
            recordId: quoteId
        },
        success: function (data) {
            //update shipping address display
            let $display = $('.fsa-display-wrapper');

            $display.find('.fsa-customer-name').html(data.CustomerName);
            $display.find('.fsa-customer-address').html(data.CustomerAddress);
            $display.find('.fsa-customer-csz').html(data.CustomerCSZ);
        }
    });
};

function LoadEditShippingAddress() {
    let addressId = $('#VM_Quote_Quote_ShippingAddress').val();
    console.log("addressId :", addressId);
    if (!addressId) {
        alert('No shipping address selected to edit.');
        $('#edit_sa_modal').modal('hide');
        return;
    }

    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-shipping-address') + '/' + addressId,
        success: function (data) {
            $('#edit_sa_id').val(data.Id);
            $('#edit_sa_client').val(data.Client);
            $('#edit_sa_customer_name').val(data.CustomerName);
            $('#edit_sa_address').val(data.Address);
            $('#edit_sa_address2').val(data.Address2);
            $('#edit_sa_city').val(data.City);
            $('#edit_sa_state').val(data.State);
            $('#edit_sa_zip').val(data.Zip);
            $('#edit_sa_phone').val(data.Phone);
            $('#edit_sa_attention').val(data.Attention);
        },
        error: function () {
            alert('Failed to load address data.');
            $('#edit_sa_modal').modal('hide');
        }
    });
};

function SaveEditShippingAddress() {
    let formData = {
        Id: $('#edit_sa_id').val(),
        Client: $('#edit_sa_client').val(),
        CustomerName: $('#edit_sa_customer_name').val(),
        Address: $('#edit_sa_address').val(),
        Address2: $('#edit_sa_address2').val(),
        City: $('#edit_sa_city').val(),
        State: $('#edit_sa_state').val(),
        Zip: $('#edit_sa_zip').val(),
        Phone: $('#edit_sa_phone').val(),
        Attention: $('#edit_sa_attention').val()
    };

    $.ajax({
        type: 'POST',
        url: ProcessUrl('update-shipping-address'),
        data: { address: formData },
        success: function (data) {
            if (data.success) {
                // Update the display
                let $display = $('.sa-display-wrapper');
                $display.find('.sa-customer-name').html(data.address.CustomerName);
                $display.find('.sa-customer-address').html(data.address.Address);
                $display.find('.sa-customer-csz').html(data.address.City + ', ' + data.address.State + ' ' + data.address.Zip);

                $('#edit_sa_modal').modal('hide');
            } else {
                alert('Failed to save address.');
            }
        },
        error: function () {
            alert('Failed to save address.');
        }
    });
};

function LoadShippingAddresses() {
    var clientId = $('#VM_Quote_Quote_Client').val();
    var quoteId = $('#VM_Quote_Quote_Id').val();

    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-shipping-addresses') + '/' + clientId + '/' + quoteId + '/quote',
        success: function (data) {
            try {
                let res = JSON.parse(data);
                $('#shipping_address_list').html(res);
            } catch (e) { }
        }
    })
};

function LoadFreightAddresses() {
    var clientId = $('#VM_Quote_Quote_Client').val();
    var quoteId = $('#VM_Quote_Quote_Id').val();

    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-freight-addresses') + '/' + clientId + '/' + quoteId + '/quote',
        success: function (data) {
            try {
                let res = JSON.parse(data);
                $('#freight_address_list').html(res);
            } catch (e) { }
        }
    })
};

function LinkItem() {
    var $btn = $(this);
    var $itemToLink = $btn.parents('.item-row');
    //find the closest .item-row above which does not have the .sub-item class
    var $linkItem = $itemToLink.closest('.item-row').prevAll('.item-row').not('.sub-item,.o-item').first();

    if ($linkItem.length > 0) {
        //link to this item
        $itemToLink.addClass('sub-item');

        let srcItemId = $itemToLink.attr('data-record-id');
        let destItemId = $linkItem.attr('data-record-id');
        let iType = 'quote';

        //set the hidden field
        $itemToLink.find('.subitem-of').val(destItemId);

        let isOutsource = $itemToLink.hasClass('o-item-row');

        if (isOutsource) {
            iType = 'o-quote';
        }

        $.ajax({
            type: 'POST',
            url: ProcessUrl('subitem-link'),
            data: {
                srcItem: srcItemId,
                destItem: destItemId,
                itemtype: iType
            },
            success: function () {
                let $unlinkBtn = $btn.parents('.item-row').find('.unlink-item-btn');
                $unlinkBtn.show();
                $btn.hide();
            }
        });
    }
};

function UnlinkItem() {
    let $btn = $(this);
    let $itemToUnlink = $btn.parents('.item-row');
    let itemId = $itemToUnlink.attr('data-record-id');
    let iType = 'quote';

    $itemToUnlink.find('.subitem-of').val('');

    let isOutsource = $itemToUnlink.hasClass('o-item-row');

    if (isOutsource) {
        iType = 'o-quote';
    }

    $.ajax({
        type: 'POST',
        url: ProcessUrl('subitem-unlink'),
        data: {
            idToUnlink: itemId,
            itemtype: iType
        },
        success: function () {
            let $linkBtn = $btn.parents('.item-row').find('.link-item-btn');
            $linkBtn.show();
            $btn.hide();
            $itemToUnlink.removeClass('sub-item');
        }
    });
};

function GenerateCustomerQuote() {
    let $clicked = $(this);
    let showTotal = $clicked.hasClass('yes');

    let recordId = $('#VM_Quote_Quote_Id').val();

    $('#quote_total_price_modal').modal('hide');

    window.open(ProcessUrl('generate-customer-quote4') + '/' + recordId + '/' + showTotal, "_blank");
};

function DuplicateQuote() {
    let quoteId = $('#VM_Quote_Quote_Id').val();
    $.ajax({
        type: 'POST',
        url: ProcessUrl('quote/duplicate'),
        data: {
            id: quoteId
        },
        success: function (data) {
            //console.log(data);
            //data will be the id of the new quote
            window.location = ProcessUrl('quote/edit/' + data);
        }
    });
};

function CustomVendorAdditionSerialization(evt) {
    evt.preventDefault();

    var form = $('#vendor_adder').serializeArray();
    let formParsed = {};
    $.each(form, function (index, field) {
        formParsed[field.name] = field.value;
    });
    $.ajax({
        type: 'POST',
        url: ProcessUrl('vendor/dynamic-create'),
        data: {
            vendor: formParsed
        },
        success: function (data) {
            try {
                let res = JSON.parse(data);

                let newSelectHtml = '';
                for (var i = 0; i < res.length; i++) {
                    newSelectHtml = (newSelectHtml + '<option value="' + res[i].Value + '">' + res[i].Text + '</option>');
                }

                //update all selects on outsourced items and ensure the current value is reselected
                $('select[name="QuoteOutsourcedItem.Vendor"]').each(function () {
                    let $sel = $(this);
                    let curVal = $(this);
                    $sel.html(newSelectHtml);
                    $sel.val(curVal);
                });
            } catch (e) { }
        }
    });
};

function CustomMaterialAdditionSerialization(evt) {
    evt.preventDefault();

    var form = $('#material_adder').serializeArray();
    let formParsed = {};
    $.each(form, function (index, field) {
        formParsed[field.name] = field.value;
    });
    $.ajax({
        type: 'POST',
        url: ProcessUrl('material/dynamic-create'),
        data: {
            material: formParsed
        },
        success: function (data) {
            try {
                $('#materials_select_modal').remove();
                GetMaterialModel();
            } catch (e) { }
        }
    });
};

function GetMaterialModel() {
    $.get(ProcessUrl('get-material-model'), function (data) {
        try {
            let res = JSON.parse(data);

            if ($('.create-wrapper')) {
                $('.create-wrapper').append(res);
            }
            if ($('.edit-wrapper').length) {
                $('.edit-wrapper').append(res);
            }
        } catch (e) { }
    });
};

function GetPressModel() {
    $.get(ProcessUrl('get-press-model'), function (data) {
        try {
            let res = JSON.parse(data);

            if ($('.create-wrapper')) {
                $('.create-wrapper').append(res);
            }
            if ($('.edit-wrapper').length) {
                $('.edit-wrapper').append(res);
            }
        } catch (e) { }

        pst = $('#press_select_table').DataTable({
            'paging': false
        });
    });
};

function SetBlindShipping(bs) {
    $('#VM_Quote_Quote_BlindShipping_Value').prop('checked', bs);
};

function LoadService() {
    var serRecId = $(this).parents('li').attr('data-record-id');

    $.ajax({
        type: 'GET',
        url: ProcessUrl('QuoteItemService/Edit/' + serRecId),
        complete: function (data) {
            //data should be the html for the service edit, so load it
            var $loadDest = $('#edit_service_modal .modal-body');
            $loadDest.html(JSON.parse(data.responseJSON));

            // Render inline timing parameters if available
            if (typeof RenderInlineTimingParameters === 'function') {
                RenderInlineTimingParameters();
            }

            //then click the button
            var btnId = '#editservice';

            $(btnId).trigger('click');
        }
    });
};

function GetEditedService() {
    let $form = $('#qis_configure_form');

    let s = {
        Id: 0,
        Notes: '',
        MakeReady: '',
        HourlyRate: '',
        ServiceTime: '',
        UserTimeOverride: false,
        ScrapPercent: '',
        Caliper: '',
        WashUp: '',
        MaterialCost: '', // = General Cost
        ScrapAmount: '',
        FormattedServiceTime: ''
    };

    s.Id = $form.find('input[name="QuoteItemService.Id"]').val();
    s.Notes = $form.find('textarea[name="QuoteItemService.Notes"]').val();
    s.MakeReady = $form.find('input[name="QuoteItemService.MakeReady"]').val();
    s.HourlyRate = $form.find('input[name="QuoteItemService.HourlyRate"]').val();
    s.ServiceTime = $form.find('input[name="ServiceTimeEditor.ServiceTime"]').val();
    s.UserTimeOverride = $form.find('input[name="ServiceTimeEditor.LockTime"]').is(':checked');
    s.ScrapPercent = $form.find('input[name="QuoteItemService.ScrapPercent"]').val();
    s.Caliper = $form.find('input[name="QuoteItemService.Caliper"]').val();
    s.WashUp = $form.find('input[name="QuoteItemService.WashUp"]').val();
    s.MaterialCost = $form.find('input[name="QuoteItemService.MaterialCost"]').val();
    s.ScrapAmount = $form.find('input[name="QuoteItemService.ScrapAmount"]').val();
    s.FormattedServiceTime = $form.find('input[name="ServiceTimeEditor.FormattedServiceTime"]').val();

    return s;
};

function SaveServiceConfiguration(e) {
    e.preventDefault();
    let service = GetEditedService();

    function saveService() {
        $.ajax({
            type: 'POST',
            url: ProcessUrl('quote-service-item/save'),
            data: {
                quoteItemService: service
            },
            complete: function () {
                $('.service-list li[data-record-id="' + service.Id + '"] .item-notes').html(service.Notes);
            }
        });
    }

    // Save timing parameters first, then save the service to avoid race condition
    if (typeof SaveInlineTimingParameters === 'function') {
        SaveInlineTimingParameters().always(saveService);
    } else {
        saveService();
    }
};

function LoadCuttingServices() {
    //load the list of cutting services and nest their possible configurations beneath them
    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-cutting-services'),
        success: function (data) {
            var res = JSON.parse(data);
            //data will contain the html
            //load into the modal body
            $('#cutting_service_modal .modal-body').html(res);
        }
    });
};

function MarkCuttingService() {
    var $t = $(this);

    $t.hasClass('selected') ?
        $t.removeClass('selected') :
        $t.addClass('selected');

    if (!$t.hasClass('selected')) {
        $t.parents('li').find('.config-list .service-config-option.selected').removeClass('selected');
    }
};

function MarkCuttingConfiguration() {
    var $t = $(this);

    $t.parents('.config-list').find('.selected').removeClass('selected');
    $t.parents('li:not(.service-config-option)').find('.service-title').addClass('selected');

    $t.hasClass('selected') ? $t.removeClass('selected') : $t.addClass('selected');
};

function AddCuttingServices() {
    var services = [];

    //delete any cutting services currently on the item
    $('.item-row[data-record-id="' + $('#sp_adder_helper').val() + '"] .is-cutting-service[value="True"]').each(function (index, item) {
        $(item).parent().find('.remove-link-li').trigger('click');
    });

    // Iterate through each selected service title
    $('.service-title.selected').each(function () {
        var serviceId = $(this).data('record-id');

        // Retrieve the first selected configuration option
        var configOption = $(this).closest('li').find('.service-config-option.selected').first();
        var configId = configOption.data('record-id');
        var configText = configOption.text().trim();

        // Add the service and its configuration option to the services array
        services.push({
            serviceId: serviceId,
            configId: configId,
            configText: configText
        });
    });

    let rowId = $('#sp_adder_helper').val();
    let recordType = 'QUOTE';

    $.ajax({
        type: 'POST',
        url: ProcessUrl('add-cutting-services'),
        data: {
            services,
            rowId,
            recordType
        },
        success: function (data) {
            //data should contain new service rows
            try {
                let res = JSON.parse(data);
                let curHtml = $('.item-row[data-record-id="' + rowId + '"] .service-list').html();

                $('.item-row[data-record-id="' + rowId + '"] .service-list').html(curHtml + res.services);
            } catch (e) { }
        },
        complete: function (data) {
            $('.save-record').trigger('click');
            $('.run-calcs').trigger('click');
        }
    });
};

function ChangeCuttingConfig() {
    let recordId = $(this).attr('data-record-id');
    $('#sp_adder_helper').val(recordId);
    $('#cutting_service_modal .current-cutting-service-item-id').val(recordId);
    $('#cutting_service_modal').modal('show');
};

function ConvertToJob() {
    if (confirm('Please confirm converting this quote to a job')) {
        //get quoteid
        let qid = $('#VM_Quote_Quote_Id').val();
        $.ajax({
            type: 'POST',
            url: ProcessUrl('Job/CreateJobFromQuote'),
            data: {
                quoteId: qid
            },
            success: function (data) {
                try {
                    let res = JSON.parse(data);
                    window.location = ProcessUrl('Job/Edit/' + res);
                } catch (e) { }
            }
        });
    }
};

function SaveThenCalculate() {
    doingCalcs = true;
    $('form#quote_builder').submit();
};

function OpenFinishingMaterials() {
    //select checkboxes for the currently selected materials
    let rowId = $(this).parents('.quote-item-row').attr('data-record-id');
    $('#sp_adder_helper').val(rowId);
    $('#finishing_selected_mats').html('');
    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-selected-finishing-materials/quote/' + rowId),
        success: function (data) {
            try {
                let res = JSON.parse(data);
                $('#finishing_material_select_table tbody input[type="checkbox"]:checked').prop('checked', false);
                for (let i = 0; i < res.length; i++) {
                    $('#finishing_material_select_table tbody input[type="checkbox"][data-material-id="' + res[i].Material + '"]').prop('checked', true);
                    $('#finishing_selected_mats').append(`<div>${res[i].Title} <i>(${res[i].Category})</i></div>`);
                }

                $('#finishing_material_select_modal .sidebar-container li:first-child').trigger('click');
            } catch (e) { }
        }
    });
};

function SaveFinishingMaterialConfigs() {
    //update-finishing-config (POST)
    //configs (List<FinishingConfig_CM>)
    //itemId (int?)
    //itemtype (string)
    let itemId = $('#sp_adder_helper').val();
    let configs = [];

    $('#finishing_material_config_table tbody tr').each(function () {
        let $row = $(this);
        configs.push({
            id: $row.attr('data-record-id'),
            value: $row.find('.ffc-input-val').val(),
            applicationValue: $row.find('select[name="ffaSel"]').val(),
            cost: $row.find('.cost-input-val').val()
        });
    });

    $.ajax({
        type: 'POST',
        url: ProcessUrl('update-finishing-config'),
        data: {
            itemtype: 'quote',
            configs
        }
    });
};

function LoadFinishingMaterialConfigs() {
    //render the configs
    let rowId = $(this).parents('.quote-item-row').attr('data-record-id');
    $('#sp_adder_helper').val(rowId);

    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-finishing-material-configs') + '/quote/' + rowId,
        beforeSend: function () {
            $('#finishing_material_config_modal table tbody').html('');
        },
        success: function (data) {
            try {
                let res = JSON.parse(data);

                for (let i = 0; i < res.length; i++) {
                    let newHtmlJson = JSON.parse(res[i].FinishingFormulaConfiguration);
                    let sel = $('.base-ffa-sl').clone();
                    let propertyKey = '';
                    let propertyValue = '';

                    // Loop through the object properties; currently only has one key/value pair
                    for (const key in newHtmlJson) {
                        if (newHtmlJson.hasOwnProperty(key)) {
                            propertyKey = key;
                            propertyValue = newHtmlJson[key];
                        }
                    }

                    // Current inventory available (stock - allocated), shown under the material name
                    let availableDisplay = '';
                    if (res[i].Stock !== null && typeof res[i].Stock !== 'undefined') {
                        let available = res[i].Stock - (res[i].Allocated || 0);
                        availableDisplay = `<br /><small class="text-muted">${available} available</small>`;
                    }

                    // Build the new HTML row
                    let newHtml = `
                                    <tr data-record-id="${res[i].Id}">
                                        <td>${res[i].Title}${availableDisplay}</td>
                                        <td>${propertyKey}<input type="text" class="ffc-input-val" value="${propertyValue}" />
                                            <select name="ffaSel">
                                                <option value="" selected></option>
                                                ${sel.html()}
                                            </select>
                                        </td>
                                        <td><input type="text" class="cost-input-val" value="${res[i].Cost}" /></td>
                                        <td>${res[i].CostType}</td>
                                    </tr>
                                `;

                    // Append the new row to the table body
                    $('#finishing_material_config_modal table tbody').append(newHtml);

                    // After appending, find the last inserted select element in the table body
                    let $newSelect = $('#finishing_material_config_modal table tbody tr:last-child select[name="ffaSel"]');

                    // Find the option that matches the CostTypeId and mark it as selected
                    $newSelect.find(`option[value="${res[i].FinishingFormulaApplication}"]`).prop('selected', true);
                }
                //render configs into config list
            } catch (e) { }
        }
    });
};

function UpdateFinishingMaterials() {
    //update-finishing-materials (POST)
    //itemId (int?)
    let itemId = $('#sp_adder_helper').val();
    //matIds (List<int>)
    let fmIds = [];
    $('#finishing_material_select_modal input[type="checkbox"][data-material-id][data-config-label][data-config-value]:checked').each(function () {
        fmIds.push($(this).attr('data-material-id'));
    });
    //itemtype (string)
    let itemtype = 'quote';

    $.ajax({
        type: 'POST',
        url: ProcessUrl('update-finishing-materials'),
        data: {
            itemId,
            fmIds,
            itemtype
        }
    }).done(function (data) {
        try {
            let res = JSON.parse(data);
            let $row = $('.quote-item-row[data-record-id="' + itemId + '"]');
            $row.find('.finishing-service-list').html(res.finishingServices);
        } catch (e) { }
    });
};

function RemoveFinishingMaterialEntry() {
    if (!confirm('This will remove the finishing material and its service row. Continue?')) return;
    let $li = $(this).parents('li');
    let serviceId = $li.attr('data-record-id');
    $.post(ProcessUrl('ajax-remove-finishing-material') + '/' + serviceId + '/quote', function () {
        $li.remove();
    });
};

function OpenServiceSettings() {
    let $li = $(this);
    $li.find('.fa-gears').trigger('click');
};

function GetShippingAddressModel() {
    let $form = $('#shipping_address_adder');
    let sa = {};
    sa.CustomerName = $form.find('input[name="CustomerName"]').val();
    sa.Address = $form.find('input[name="Address"]').val();
    sa.Address2 = $form.find('input[name="Address2"]').val();
    sa.City = $form.find('input[name="City"]').val();
    sa.State = $form.find('select[name="State"]').val();
    sa.Zip = $form.find('input[name="Zip"]').val();
    sa.Phone = $form.find('input[name="Phone"]').val();
    sa.Attention = $form.find('input[name="Attention"]').val();
    sa.Client = $('#VM_Quote_Quote_Client').val();

    return sa;
};

function GetFreightAddressModel() {
    let $form = $('#freight_shipping_address_adder');
    let sa = {};
    sa.CustomerName = $form.find('input[name="CustomerName"]').val();
    sa.Address = $form.find('input[name="Address"]').val();
    sa.Address2 = $form.find('input[name="Address2"]').val();
    sa.City = $form.find('input[name="City"]').val();
    sa.State = $form.find('select[name="State"]').val();
    sa.Zip = $form.find('input[name="Zip"]').val();
    sa.Phone = $form.find('input[name="Phone"]').val();
    sa.Attention = $form.find('input[name="Attention"]').val();
    sa.Client = $('#VM_Quote_Quote_Client').val();

    return sa;
};

function CustomShippingAddressSerialization(evt) {
    evt.preventDefault();

    var form = GetShippingAddressModel();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('dynamic-shipping-address'),
        data: {
            address: form
        }
    });
};

function CustomFreightAddressSerialization(evt) {
    evt.preventDefault();

    var form = GetFreightAddressModel();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('dynamic-freight-address'),
        data: {
            address: form
        }
    });
};

function ReRenderRowServices(rowId) {
    let $row = $('.quote-item-row[data-record-id="' + rowId + '"]');
    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-service-list/quote/' + rowId),
        success: function (data) {
            try {
                let res = JSON.parse(data);
                $row.find('.prepress-service-list').html('');
                $row.find('.printing-service-list').html('');
                $row.find('.cutting-service-list').html('');
                $row.find('.finishing-service-list').html('');
                $row.find('.prepress-service-list').html(res.prepressServices);
                $row.find('.printing-service-list').html(res.printingServices);
                $row.find('.cutting-service-list').html(res.cuttingServices);
                $row.find('.finishing-service-list').html(res.finishingServices);
            } catch (e) { }
        }
    });
};

function ClearShippingAddress() {
    let recordId = $('#VM_Quote_Quote_Id').val();
    let itemtype = 'quote';

    $.ajax({
        type: 'POST',
        url: ProcessUrl('clear-shipping-address'),
        data: {
            recordId,
            itemtype
        },
        success: function () {
            //update shipping address display
            let $display = $('.sa-display-wrapper');

            $display.find('.sa-customer-name').html('');
            $display.find('.sa-customer-address').html('');
            $display.find('.sa-customer-csz').html('');
        }
    });
};

function ClearFreightAddress() {
    let recordId = $('#VM_Quote_Quote_Id').val();
    let itemtype = 'quote';

    $.ajax({
        type: 'POST',
        url: ProcessUrl('clear-freight-address'),
        data: {
            recordId,
            itemtype
        },
        success: function () {
            //update shipping address display
            let $display = $('.fsa-display-wrapper');

            $display.find('.fsa-customer-name').html('');
            $display.find('.fsa-customer-address').html('');
            $display.find('.fsa-customer-csz').html('');
        }
    });
};

function OpenPressSpeedSettings() {
    //get the press id
    let pressId = $(this).attr('data-press-id');
    //load the speeds for the press
    //set the selected speed

    $.ajax({
        type: 'GET',
        url: ProcessUrl('item-load-speeds/' + pressId),
        success: function (data) {
            $('#press_speed_select_modal tbody').html(data);
        },
        complete: function () {
            //find the selected radio button in #press_speed_select_modal tbody and click it
            var $selected = $('#press_speed_select_modal tbody input[type="radio"]:checked');
            if ($selected.length > 0) {
                $selected.trigger('click');
            }

            $('#press_select_modal').modal('hide');
            $('#press_speed_select_modal').modal('show');
        }
    });
};

function CloseSpeedSettingsAndOpenPressSelect() {
    $('#press_speed_select_modal').modal('hide');
    $('#press_select_modal').modal('show');
};

function UpdateSelectedPressSpeed() {
    //update the record
    let speedId = $('#press_speed_select_table input:checked').parents('tr').attr('data-record-id');
    let speedUnitVal = $('#press_speed_select_table input:checked').parents('tr').attr('data-speed-unit-val');
    let speedUnitText = $('#press_speed_select_table input:checked').parents('tr').attr('data-speed-unit-text');
    let speedValue = $('#press_speed_select_table input:checked').parents('tr').attr('data-speed-val');

    $('#print_speed_ref_sl').val(speedUnitVal);
    //update the press table display
    let itemId = $('#sp_adder_helper').val();
    //selected press -> press speed text -> new value
    let $item = $('.quote-item-row[data-record-id="' + itemId + '"]');
    $item.find('.selected-press-speed-display .speed').html(speedValue);
    $item.find('.selected-press-speed-display .speed-unit').html(speedUnitText);
    $item.find('.selected-speed').val(speedId);
};

function SubmitMaterialConfig() {
    let rowId = $('#sp_adder_helper').val();
    let itemType = 'quote';
    let $modal = $('#material_config_modal');

    let mConfig = {
        ColorFrontProfile: $modal.find('.press-color-string-front').val(),
        ColorBackProfile: $modal.find('.press-color-string-back').val(),
        ColorFrontCount: $modal.find('.press-color-count-front-inp').val(),
        ColorBackCount: $modal.find('.press-color-count-back-inp').val(),
        ColorFrontNotes: $modal.find('.press-color-notes-front-inp').val(),
        ColorBackNotes: $modal.find('.press-color-notes-back-inp').val(),
        RunLength: $modal.find('.material-run-size-l').val(),
        RunWidth: $modal.find('.material-run-size-w').val(),
        Sheets: $modal.find('.sheets').val(),
        ScrapSheets: $modal.find('.scrap-sheets').val(),
        RunSheets: $modal.find('.run-sheets').val(),
        Up: $modal.find('.up-value').val(),
        DoubleInk: $modal.find('.double-ink-checkbox').is(':checked'),
        Collate: $modal.find('.collate-checkbox').is(':checked'),
        Staple: $modal.find('.staple-checkbox').is(':checked'),
        Numbering: $modal.find('.numbering-checkbox').is(':checked'),
        MaterialCost: $modal.find('.material-cpu').val(),
        Out: $modal.find('.mc-out').val(),
        BuySheets: $modal.find('.buy-sheets').val()
    };

    //also update $row.find('.current-mrs') with the new value (i.e. 20x50) so that the value is reinforced when the record is saved (or on calc which saves)
    let $row = $('.quote-item-row[data-record-id="' + rowId + '"]');
    $row.find('.current-mrs').val(mConfig.RunLength + 'x' + mConfig.RunWidth);

    $.ajax({
        type: 'POST',
        url: ProcessUrl('update-material-config'),
        data: {
            config: mConfig,
            itemId: rowId,
            itemType
        },
        complete: function () {
            $row.find('.stock-row .run-size').html(mConfig.RunLength + 'x' + mConfig.RunWidth);
            $row.find('.stock-row .sheets-display').html(mConfig.Sheets);
            $row.find('.stock-row .out-display').html(mConfig.Out);
            $row.find('.stock-row .buy-sheets').html(mConfig.BuySheets);
            $row.find('.stock-row .calced-up-val').html(mConfig.Up);
            $row.find('.stock-row .front-color-display').html(mConfig.ColorFrontProfile + '  ' + mConfig.ColorFrontNotes);
            $row.find('.stock-row .back-color-display').html(mConfig.ColorBackProfile + '  ' + mConfig.ColorBackNotes);
            $row.find('.stock-row .selected-material-cost').html('$' + mConfig.MaterialCost);

            // Show sheet override indicator if already enabled or sheets were changed
            let wasOverridden = $row.find('.sheet-override-display').is(':visible');
            let sheetsChanged =
                mConfig.Sheets != $modal.data('orig-sheets') ||
                mConfig.ScrapSheets != $modal.data('orig-scrap') ||
                mConfig.BuySheets != $modal.data('orig-buy');

            if (wasOverridden || sheetsChanged) {
                $row.find('.sheet-override-display').show();
                $('#material_config_modal .clear-sheet-override').show();
            }

            // Toggle double ink indicator
            if (mConfig.DoubleInk) {
                $row.find('.double-ink-display').show();
            } else {
                $row.find('.double-ink-display').hide();
            }
        }
    });
};

function LoadMaterialConfig() {
    let itemId = $(this).parents('.quote-item-row').attr('data-record-id');
    $('#sp_adder_helper').val(itemId);

    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-material-config') + '/' + itemId + '/quote',
        success: function (data) {
            let res = JSON.parse(data);
            let $modal = $('#material_config_modal');
            $modal.find('.press-color-string-front').val(res.ColorFrontProfile);
            $modal.find('.press-color-string-back').val(res.ColorBackProfile);
            $modal.find('.press-color-count-front-inp').val(res.ColorFrontCount);
            $modal.find('.press-color-count-back-inp').val(res.ColorBackCount);
            $modal.find('.press-color-notes-back-inp').val(res.ColorBackNotes);
            $modal.find('.press-color-notes-front-inp').val(res.ColorFrontNotes);
            $modal.find('.material-run-size-l').val(res.RunLength);
            $modal.find('.material-run-size-w').val(res.RunWidth);
            $modal.find('.sheets').val(res.Sheets);
            $modal.find('.scrap-sheets').val(res.ScrapSheets);
            $modal.find('.run-sheets').val(res.RunSheets);
            $modal.find('.buy-sheets').val(res.BuySheets);
            $modal.find('.up-value').val(res.Up);
            $modal.find('.mct-display').html(res.MaterialCostType);
            $modal.find('.material-cpu').val(res.MaterialCost);
            $modal.find('.mc-out').val(res.Out);

            // Store original sheet values for comparison on submit
            $modal.data('orig-sheets', res.Sheets);
            $modal.data('orig-scrap', res.ScrapSheets);
            $modal.data('orig-buy', res.BuySheets);

            if (res.UserSheetOverride) {
                $modal.find('.clear-sheet-override').show();
            } else {
                $modal.find('.clear-sheet-override').hide();
            }

            // Also update the row indicators
            let $row = $('.quote-item-row[data-record-id="' + itemId + '"]');
            if (res.UserSheetOverride) {
                $row.find('.sheet-override-display').show();
            } else {
                $row.find('.sheet-override-display').hide();
            }

            if (res.DoubleInk) {
                $row.find('.double-ink-display').show();
            } else {
                $row.find('.double-ink-display').hide();
            }

            // Set checkbox states on the modal
            $modal.find('.double-ink-checkbox').prop('checked', res.DoubleInk);
            $modal.find('.collate-checkbox').prop('checked', res.Collate);
            $modal.find('.staple-checkbox').prop('checked', res.Staple);
            $modal.find('.numbering-checkbox').prop('checked', res.Numbering);

            $modal.modal('show');
        }
    });
};

function IncrementUpValue() {
    let $row = $(this).parents('.quote-item-row');
    let rId = $row.attr('data-record-id');
    let curUp = Number($row.find('.calced-up-val').html().trim());
    $row.find('.calced-up-val').html(curUp + 1);
};

function DecrementUpValue() {
    let $row = $(this).parents('.quote-item-row');
    let rId = $row.attr('data-record-id');
    let curUp = Number($row.find('.calced-up-val').html().trim());
    $row.find('.calced-up-val').html(curUp - 1);
};

function HandleUpValueCheckbox($itemRow) {
    let curRecordId = $itemRow.attr('data-record-id');
    let curRecordWidth = $itemRow.find('input.item-width').val();
    let curRecordLength = $itemRow.find('input.item-length').val();

    if (curRecordId === itemWidthLengthCur.recordId) {
        if (itemWidthLengthCur.length !== curRecordLength || itemWidthLengthCur.width !== curRecordWidth) {
            $itemRow.find('.up-value-locked').prop('checked', false);
        }
    }
};

function UpdateFinishSizeDisplay() {
    let $row = $(this).parents('.quote-item-row');
    HandleUpValueCheckbox($row);
    let w = $row.find('input.item-width').val();
    let l = $row.find('input.item-length').val();
    let fsd = l + ' x ' + w;

    $row.find('.finish-size-display').html(fsd);
};

function ArtworkProvidedToggle() {
    let $cb = $(this);
    let checked = $cb.is(':checked');
    let $parent = $cb.parents('.item-right-ticket-wrapper');

    if (!checked) {
        //console.log('hiding time input');
        $parent.find('.design-time-input').addClass('d-none');
        $parent.find('.design-time-input input').val(0);
    } else {
        //console.log('showing time input');
        $parent.find('.design-time-input').removeClass('d-none');
    }
};

function DoubleInkHelper() {
    let c = $(this).is(':checked');
    let rowId = $('#sp_adder_helper').val();
    let $row = $('.quote-item-row[data-record-id="' + rowId + '"]');

    $row.find('.press-double-ink').prop('checked', c);
};

function CollateHelper() {
    let c = $(this).is(':checked');

    let rowId = $('#sp_adder_helper').val();
    let $row = $('.quote-item-row[data-record-id="' + rowId + '"]');

    $row.find('.press-collate').prop('checked', c);
};

function StapleHelper() {
    let c = $(this).is(':checked');

    let rowId = $('#sp_adder_helper').val();
    let $row = $('.quote-item-row[data-record-id="' + rowId + '"]');

    $row.find('.press-staple').prop('checked', c);
};

function FilterFinishingMaterials() {
    let catName = $(this).html().trim();
    $('tr.fin-material-row').hide();
    $('tr.fin-material-row[data-cat-name="' + catName + '"]').show();
};

function ClearSheetOverride() {
    let rId = $('#sp_adder_helper').val();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('clear-sheet-override'),
        data: {
            itemtype: 'quote',
            itemId: rId
        },
        success: function () {
            $('#material_config_modal .clear-sheet-override').hide();
            // Also hide the row indicator
            let $row = $('.quote-item-row[data-record-id="' + rId + '"]');
            $row.find('.sheet-override-display').hide();
            alert('Sheet override cleared');
        }
    });
};

function GoToJob() {
    let jobId = $(this).attr('data-record-id');
    window.location = ProcessUrl('Job/Edit') + '/' + jobId;
};

function LoadReorderItemModal() {
    let quoteId = $('#VM_Quote_Quote_Id').val();

    $.ajax({
        type: 'GET',
        url: ProcessUrl('reorder-item-modal') + '/' + quoteId + '/quote',
        success: function (data) {
            let $itemList = $('#item_reorder_modal .item-list');
            $itemList.html('');

            for (let i = 0; i < data.length; i++) {
                let itemHtml =
                    `
                        <li data-record-id="${data[i].Id}" class="py-3">
                            <div class="container">
                                <div class="row">
                                    <div class="col-12">
                                        <span class="ff-poppins fs-14">${data[i].Description}</span>
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col-12">
                                        <span class="ff-poppins fs-14 fw-bold ps-3">Qty: ${data[i].Quantity}</span>
                                        <span class="ff-poppins fs-14 fw-bold ps-3">Size: ${data[i].Length}x${data[i].Width}</span>
                                    </div>
                                </div>
                            </div>
                        </li>
                    `;

                $itemList.append(itemHtml);
            }

            $itemList.sortable();
        },
        error: function (err) {
            alert('error LoadReorderItemModal()');
        }
    });
};

function SubmitItemReorder() {
    let itemIds = [];
    let ordinals = [];

    let ordinal = 1;

    $('#item_reorder_modal .item-list li').each(function () {
        let $item = $(this);

        itemIds.push($item.attr('data-record-id'));
        ordinals.push(ordinal);

        ordinal++;
    });

    $.ajax({
        type: 'POST',
        url: ProcessUrl('update-item-order'),
        data: {
            itemIds,
            ordinals,
            itemType: 'quote'
        },
        success: function (data) {
            //visual reorder starts here
            $('#job_items').fadeOut('fast', function () {
                $('#job_items').html('');

                $('#job_items').html(data);
                $('#item_reorder_modal').modal('hide');
                $('#job_items').fadeIn('fast');

                //rerun calcs to update price list
                $('.run-calcs').trigger('click');
            });
        },
        error: function (err) {
            alert(err);
        }
    });
};

function DynamicClientCreate() {
    let client = {};

    let $f = $('#dynamic_client_modal .client-form');

    client.Title = $f.find('input[name="Client.Title"]').val();
    client.BlindShipping = $f.find('input[name="Client.BlindShipping.Value"]').is(':checked');
    client.TaxExempt = $f.find('input[name="Client.TaxExempt.Value"]').is(':checked');
    client.IsGolfClient = $f.find('input[name="Client.IsGolfClient.Value"]').is(':checked');
    client.Address = $f.find('input[name="Client.Address"]').val();
    client.AddressAlt = $f.find('input[name="Client.AddressAlt"]').val();
    client.City = $f.find('input[name="Client.City"]').val();
    client.USStateId = $f.find('select[name="Client.USStateId"]').val();
    client.Zip = $f.find('input[name="Client.Zip"]').val();
    client.AREmail = $f.find('input[name="Client.AREmail"]').val();
    client.Notes = $f.find('textarea[name="Client.Notes"]').val();
    client.Markup = $f.find('input[name="Client.Markup"]').val();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('dynamic-client-create'),
        data: {
            c: client
        },
        success: function (data) {
            $('#dynamic_client_modal').modal('hide');
            alert('Client added');
        },
        error: function (err) {
            //console.log(err);
            alert('Error adding client');
        }
    })
};

function DynamicContactCreate() {
    let contact = {};

    let $f = $('#dynamic_contact_modal .contact-form');

    contact.CusNo = $f.find('#dmc_cus_no').val();
    contact.First = $f.find('#dmc_first_name').val();
    contact.Last = $f.find('#dmc_last_name').val();
    contact.Phone = $f.find('#dmc_phone').val();
    contact.Phone2 = $f.find('#dmc_phone2').val();
    contact.Email = $f.find('#dmc_email').val();
    contact.ContactNote = $f.find('#dmc_contact_note').val();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('dynamic-contact-create'),
        data: {
            c: contact
        },
        success: function (data) {
            $('#dynamic_contact_modal').modal('hide');
            alert('Contact added');
        },
        error: function (err) {
            //console.log(err);
            alert('Error adding contact');
        }
    })
};

function CheckForClient() {
    //ensure a client is selected
    let cId = $('#VM_Quote_Quote_Client').val();
    let cName = $('#client_autocomplete').val();

    //if not selected, hide the modal and inform the user
    if (cId === '') {
        //modals that require a selected client
        $('#dynamic_contact_modal').modal('hide');
        $('#client_billing_modal').modal('hide');

        alert('Please select a client first');
    }
    //if selected, set the dmc_cus_no input on the modal
    else {
        $('#dmc_cus_no').val(cId);
        $('#dynamic_contact_modal .modal-header').html('Add Contact to ' + cName);
        $('#client_billing_modal .modal-header').html('Billing Info for ' + cName);
    }
};

function LoadClientBillingDetails() {
    let cId = $('#VM_Quote_Quote_Client').val();

    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-client-billing-details/' + cId),
        success: function (data) {
            if (data) {
                //update the inputs
                $('#cb_state').empty();
                for (let i = 0; i < data.states.length; i++) {
                    $('#cb_state').append(`<option value="${data.states[i].Value}">${data.states[i].Text}</option>`)
                }

                $('#cb_address').val(data.BillingAddress);
                $('#cb_city').val(data.BillingCity);
                $('#cb_zip').val(data.BillingZip);
                $('#cb_aremail').val(data.AREmail);

                $('#cb_state').val(data.BillingUSState);
            }
        },
        error: function (err) {
            //console.log(err);
            alert('Error');
        }
    });
};

function SaveClientBillingInfo() {
    let cId = $('#VM_Quote_Quote_Client').val();
    let $details = $('#client_billing_modal .billing-form');

    let client = {
        Id: cId,
        BillingAddress: $details.find('#cb_address').val(),
        BillingCity: $details.find('#cb_city').val(),
        BillingZip: $details.find('#cb_zip').val(),
        BillingUSState: $details.find('#cb_state').val(),
        AREmail: $details.find('#cb_aremail').val()
    };

    $.ajax({
        type: 'POST',
        url: ProcessUrl('save-client-billing-info'),
        data: {
            client
        },
        success: function (data) {
            //console.log(data);
            alert('Billing details saved');
        },
        error: function (err) {
            //console.log(err);
            alert('error');
        }
    });
};

function SelectQuickItems() {
}

function RevertMaterialPrice() {
    let rowId = $('#sp_adder_helper').val();
    let $row = $('.quote-item-row[data-record-id="' + rowId + '"]');
    let materialId = $row.find('.selectedmaterial').val();
    //ajax load the base material cost
    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-base-material-price') + '/' + materialId,
        success: function (data) {
            //insert into material config modal price field
            $('#material_config_modal .material-cpu').val(data.price);
            $row.find('.stock-row .selected-material-cost').html('$' + data.price);
        },
        error: function () {
            alert('Error getting material base price');
        }
    });
    //user will confirm/save modal which will save the value as normal
};

function DeleteSelectedDocuments() {
    //get the selected ids of the documents
    let ids = new Array();

    $('#document_list_modal .document-list li input:checked').each(function () {
        ids.push($(this).parents('li').attr('data-doc-id'));
    });

    //ajax to backend as list of ints
    $.ajax({
        type: 'POST',
        url: ProcessUrl('delete-quote-docs'),
        data: {
            ids
        },
        success: function () {
            //documents deleted
            $('#document_list_modal').modal('hide');
            alert('Documents deleted');
        },
        error: function () {
            //issue deleting documents
            alert('There was an issue deleting the documents; contact admin');
        }
    });
};

function ClearContactSelection() {
    //clear input field
    $('#contact_autocomplete').val('');
    $('#VM_Quote_Quote_Contact').val('');

    //record must be saved to fully clear contact
};

// Function to load contact data into the modal
// Function to load contact data into the modal
function loadContactData(contactId) {
    if (!contactId) {
        // Clear form for new contact
        $('#contactEditorForm')[0].reset();
        $('#contactId').val('');
        return;
    }

    console.log('Loading contact data for ID:', contactId); // Debug log

    $.ajax({
        url: ProcessUrl('Customers/GetContact'),
        type: 'GET',
        data: { id: contactId },
        success: function (contact) {
            console.log('Contact data received:', contact); // Debug log

            if (contact) {
                $('#contactId').val(contact.Id);
                $('#cusNo').val(contact.CusNo);
                $('#firstName').val(contact.First);
                $('#lastName').val(contact.Last);
                $('#phone').val(contact.Phone);
                $('#phone2').val(contact.Phone2);
                $('#cell').val(contact.Cell);
                $('#email').val(contact.Email);
                $('#contactNote').val(contact.ContactNote);

                // Set hidden fields
                $('#fax').val(contact.Fax);
                $('#position').val(contact.Position);
                $('#conTag').val(contact.ConTag);
                $('#sendEmail').val(contact.SendEmail);
                $('#sendSurvey').val(contact.SendSurvey);
                $('#keyContact').val(contact.KeyContact);
                $('#shipTo').val(contact.ShipTo);
            }
        },
        error: function (xhr, status, error) {
            console.log('Error loading contact data:', error); // Debug log
            alert('Error loading contact data: ' + error);
        }
    });
}

// Function to save contact data
function saveContact() {
    var formData = {
        Id: $('#contactId').val(),
        CusNo: $('#cusNo').val(),
        First: $('#firstName').val(),
        Last: $('#lastName').val(),
        Phone: $('#phone').val(),
        Phone2: $('#phone2').val(),
        Cell: $('#cell').val(),
        Fax: $('#fax').val(),
        Email: $('#email').val(),
        Position: $('#position').val(),
        ContactNote: $('#contactNote').val(),
        ConTag: $('#conTag').val(),
        SendEmail: $('#sendEmail').is(':checked'),
        SendSurvey: $('#sendSurvey').is(':checked'),
        KeyContact: $('#keyContact').is(':checked'),
        ShipTo: $('#shipTo').is(':checked')
    };

    $.ajax({
        url: ProcessUrl('Customers/SaveContact'), // Adjust URL to your controller action
        type: 'POST',
        data: formData,
        success: function (response) {
            if (response.success) {
                $('#contactEditorModal').modal('hide');
                // Optionally refresh the parent page or update contact display
                if (typeof updateContactDisplay === 'function') {
                    updateContactDisplay(response.contact);
                }
                alert('Contact saved successfully');
            } else {
                alert('Error saving contact: ' + response.message);
            }
        },
        error: function () {
            alert('Error saving contact');
        }
    });
}

// Event handler for when modal is shown (using event delegation)
$(document).on('show.bs.modal', '#contactEditorModal', function (event) {
    // Get the selected contact ID from the hidden field
    var contactId = null;

    // Check for Job context
    var jobContactField = $('input[name="VM_Job.Job.Contact"]');
    if (jobContactField.length > 0) {
        contactId = jobContactField.val();
        console.log('Found Job contact ID:', contactId); // Debug log
    }

    // Check for Quote context if not found in Job
    if (!contactId) {
        var quoteContactField = $('input[name="VM_Quote.Quote.Contact"]');
        if (quoteContactField.length > 0) {
            contactId = quoteContactField.val();
            console.log('Found Quote contact ID:', contactId); // Debug log
        }
    }

    console.log('Final contact ID to load:', contactId); // Debug log

    if (contactId && contactId !== '') {
        loadContactData(contactId);
    } else {
        console.log('No contact ID found, clearing form'); // Debug log
        // Clear form for new contact
        $('#contactEditorForm')[0].reset();
        $('#contactId').val('');
    }
});

// Phone number formatting
function formatPhoneNumber(input) {
    var value = input.value.replace(/\D/g, '');
    var formattedValue = '';

    if (value.length >= 6) {
        formattedValue = value.substring(0, 3) + '-' + value.substring(3, 6) + '-' + value.substring(6, 10);
    } else if (value.length >= 3) {
        formattedValue = value.substring(0, 3) + '-' + value.substring(3);
    } else {
        formattedValue = value;
    }

    input.value = formattedValue;
}

function StoreCurrentLengthWidthValues() {
    //itemWidthLengthCur.recordId,.Length,.Width

    let $inp = $(this);
    let $row = $inp.parents('.item-row');
    let recordId = $row.attr('data-record-id');
    let iWidth = $row.find('input.item-width').val();
    let iLength = $row.find('input.item-length').val();

    itemWidthLengthCur.recordId = recordId;
    itemWidthLengthCur.length = iLength;
    itemWidthLengthCur.width = iWidth;
}

// Apply phone formatting to phone fields (using event delegation)
$(document).on('.contact-editor-modal input', '.contact-editor-modal #phone, .contact-editor-modal #phone2, .contact-editor-modal #cell, .contact-editor-modal #fax', function () {
    formatPhoneNumber(this);
});

$(function () {
    GetMaterialModel();
    GetPressModel();
    //GetFinishingMaterialsModel();

    $('#client_autocomplete').autocomplete({
        source: function (request, response) {
            $.ajax({
                url: ProcessUrl("AutocompleteClient"),
                type: 'GET',
                data: {
                    'q': request.term
                },
                beforeSend: function () {
                    $('.contact-list-wrapper').hide();
                },
                success: function (data) {
                    let d = JSON.parse(data);
                    response($.map(d, function (item) {
                        return {
                            label: item.Title,
                            value: item.Title,
                            recordId: item.Id,
                            contactArr: item.Contacts,
                            markup: item.Markup,
                            defaultAddressId: item.DefaultAddress,
                            creditHold: item.CreditHold
                        }
                    }));
                }
            });
        },
        select: function (event, ui) {
            $('#VM_Quote_Quote_Client').val(ui.item.recordId);
            //load the contacts
            LoadClientContacts();

            $('.markup-input').val((ui.item.markup * 100));
            $('#VM_Quote_Quote_MarkupOverride').val(ui.item.markup);

            // Update credit hold state
            let onHold = ui.item.creditHold === true;
            $('#client_credit_hold').val(onHold ? 'true' : 'false');
            if (onHold) {
                $('#credit-hold-banner').removeClass('d-none');
            } else {
                $('#credit-hold-banner').addClass('d-none');
            }
        }
    });

    $('#contact_autocomplete').autocomplete({
        source: function (request, response) {
            $.ajax({
                url: ProcessUrl("AutocompleteContact"),
                type: 'GET',
                data: {
                    'q': request.term,
                    'clientId': $('#VM_Quote_Quote_Client').val()
                },
                success: function (data) {
                    try {
                        let d = JSON.parse(data);
                        response($.map(d, function (item) {
                            return { label: (item.FirstName + " " + item.LastName), value: (item.FirstName + " " + item.LastName), recordId: item.Id }
                        }));
                    } catch (e) { }
                }
            });
        },
        select: function (event, ui) {
            $('#VM_Quote_Quote_Contact').val(ui.item.recordId);
        }
    });

    $('#add_document_modal form').submit(function (e) {
        e.preventDefault();

        var formData = new FormData(this);
        //new
        //var files = $('#file_inp')[0].files;

        //for (var i = 0; i < files.length; i++) {
        //    formData.append('theDocs', files[i]);
        //}
        //end new
        $.ajax({
            type: 'POST',
            url: $(this).attr('action'),
            data: formData,
            processData: false,
            contentType: false
        });
    });

    if ($('#VM_Quote_Quote_Client').val() !== '') {
        $('#contact_autocomplete').prop('disabled', false);
    }

    $('.service-list').each(function () {
        let $list = $(this);
        $list.sortable();
    });

    //set sub-navbar width (window width - sidenav width)
    let sidenavWidth = $('.sidenav').width();
    let windowWidth = $(window).width();
    $('.edit-quote-wrapper').width(windowWidth - sidenavWidth);

    setHeaderWidth();

    $(window).on('resize', setHeaderWidth);

    $('input,select,textarea').on('focus', ChangeDetector);
    $('input,select,textarea').on('blur', ChangeDetectorCheck);
})
    .on('click', '#materials_select input[type="checkbox"]', AddButtonHandler)
    .on('click', '.select-material-button', SelectMaterials)
    .on('click', '.select-press-btn', SelectPresses)
    .on('click', '.add-item-btn', AddItem)
    .on('click', '#job_items .remove-btn', RemoveItem)
    .on('submit', '#quote_builder:not(#qis_configure_form)', CustomFormSerialization)
    .on('click', '.builder-product-add', AddMaterialToItem)
    .on('click', '.builder-press-add', AddPressToItem)
    .on('blur', '#VM_Quote_Quote_JobNumber', CheckJobNumber)
    .on('click', '.collapser-parent', ExpandCollapseList)
    .on('click', '.builder-prepress-service-add', AddPrepressServicesToItem)
    .on('click', '.builder-cutting-service-add', AddCuttingServicesToItem)
    .on('click', '.builder-finishing-service-add', AddFinishingServicesToItem)
    .on('click', '#materials_select_table tbody tr', SelectMaterialRow)
    .on('click', '#press_select_table tbody tr .press-title,#press_select_table tbody tr .speed-settings-text', SelectPressRow)
    .on('click', '.show-hide-contacts', ShowHideContacts)
    .on('click', '.view-layout-btn', ViewLayout)
    .on('click', '#press_speed_select_table input[type="radio"]', SpeedRadioHandler)
    .on('click', '.update-press-speed', UpdateItemSpeedInfo)
    .on('click', '#materials_select_modal .sidebar-container li', LoadMaterials)
    .on('shown.bs.modal', '#materials_select_modal', SelectQuickItems)
    .on('click', '.doc-link', LoadItemDocuments)
    .on('click', '.add-doc-btn', AddDocument)
    .on('click', '.cancel-add-doc', AddDocCancel)
    .on('click', '.this-item', DeleteFromThisItem)
    .on('click', '.all-items', DeleteFromAllItems)
    .on('click', '.document-list .fa-download', DownloadFile)
    .on('click', '.adjust-mat-cost-btn', AdjustMaterialCost)
    .on('click', '.submit-mat-price-override-btn', SubmitMaterialPriceOverride)
    .on('click', '.contact-list input[type="radio"]', UpdateContact)
    .on('click', '.save-record', SaveRecord)
    .on('click', '.dup-item-btn', CopyItem)
    .on('shown.bs.modal', '#mat_price_override_modal', FocusCostField)
    .on('click', '.add-outsourced-item-btn', AddOutsourcedItem)
    .on('click', '.view-quote-ticket-btn', ViewQuoteTicket)
    .on('click', '.select-finishing-materials-btn', SetHelperToRowId)
    .on('click', '.override-sheet-amount', OverrideSheetCount)
    .on('click', 'ul .remove-link-li', RemoveServiceEntry)
    .on('click', '.remove-finishing-material-li', RemoveFinishingMaterialEntry)
    .on('click', 'input[name="pickup_ship"]', PickupShipToggle)
    .on('click', '.update-shipping-address-btn', UpdateShippingAddress)
    .on('click', '.update-freight-shipping-address-btn', UpdateFreightShippingAddress)
    .on('show.bs.modal', '#shipping_address_modal', LoadShippingAddresses)
    .on('show.bs.modal', '#freight_shipping_address_modal', LoadFreightAddresses)
    .on('click', '.link-item-btn', LinkItem)
    .on('click', '.unlink-item-btn', UnlinkItem)
    .on('click', '.duplicate-quote-btn', DuplicateQuote)
    .on('click', '.save-dynamic-vendor', CustomVendorAdditionSerialization)
    .on('click', '.save-dynamic-material', CustomMaterialAdditionSerialization)
    .on('click', '.quote-item-row .service-list .fa-gears', LoadService)
    .on('submit', '#qis_configure_form', SaveServiceConfiguration)
    .on('blur', 'input[name="ServiceTimeEditor.FormattedServiceTime"]', ConvertFormattedToDecimal)
    .on('blur', 'input[name="ServiceTimeEditor.ServiceTime"]', ConvertServiceTimeToFormatted)
    .on('show.bs.modal', '#cutting_service_modal', LoadCuttingServices)
    .on('click', '.cutting-service-list .service-title', MarkCuttingService)
    .on('click', '.cutting-service-list .service-config-option', MarkCuttingConfiguration)
    .on('click', '.add-cutting-services', AddCuttingServices)
    .on('click', '.change-cutting-config', ChangeCuttingConfig)
    .on('click', '.convert-to-job-btn', ConvertToJob)
    .on('click', '.run-calcs', SaveThenCalculate)
    .on('click', '.finishing-materials-config-btn', LoadFinishingMaterialConfigs)
    .on('click', '.finishing-material-config-done', SaveFinishingMaterialConfigs)
    .on('click', '.finishing-materials-btn', OpenFinishingMaterials)
    .on('click', '.builder-finishing-materials-add', UpdateFinishingMaterials)
    .on('dblclick', '.service-list li', OpenServiceSettings)
    .on('submit', '#shipping_address_adder', CustomShippingAddressSerialization)
    .on('submit', '#freight_shipping_address_adder', CustomFreightAddressSerialization)
    .on('click', '.clear-address-btn', ClearShippingAddress)
    .on('click', '.clear-freight-address-btn', ClearFreightAddress)
    .on('click', '.pre-ser-add-btn,.cut-ser-add-btn,.fin-ser-add-btn', SetHelperToRowId)
    .on('click', '.press-speed-settings .open-settings', OpenPressSpeedSettings)
    .on('click', '#press_speed_select_modal button', CloseSpeedSettingsAndOpenPressSelect)
    .on('click', '#press_speed_select_modal .update-press-speed', UpdateSelectedPressSpeed)
    .on('click', '.material-config-finish', SubmitMaterialConfig)
    .on('click', '.open-material-config', LoadMaterialConfig)
    .on('click', '.up-up-btn', IncrementUpValue)
    .on('click', '.up-down-btn', DecrementUpValue)
    .on('focus', 'input.item-length, input.item-width', StoreCurrentLengthWidthValues)
    .on('blur', 'input.item-length,input.item-width', UpdateFinishSizeDisplay)
    .on('click', '.artwork-provided', ArtworkProvidedToggle)
    .on('click', '#material_config_modal .double-ink-checkbox', DoubleInkHelper)
    .on('click', '#material_config_modal .collate-checkbox', CollateHelper)
    .on('click', '#material_config_modal .staple-checkbox', StapleHelper)
    .on('click', '#finishing_material_select_modal .modal-body .sidebar-container li', FilterFinishingMaterials)
    .on('click', '.clear-sheet-override', ClearSheetOverride)
    .on('click', '.job-from-quote', GoToJob)
    .on('show.bs.modal', '#item_reorder_modal', LoadReorderItemModal)
    .on('click', '.submit-item-reorder-btn', SubmitItemReorder)
    .on('click', '.modal.yn-modal .modal-body i', GenerateCustomerQuote)
    .on('click', '.save-dynamic-client', DynamicClientCreate)
    .on('click', '.save-dynamic-contact', DynamicContactCreate)
    .on('shown.bs.modal', '#dynamic_contact_modal', CheckForClient)
    .on('shown.bs.modal', '#client_billing_modal', CheckForClient)
    .on('shown.bs.modal', '#client_billing_modal', LoadClientBillingDetails)
    .on('click', '.save-client-billing-info', SaveClientBillingInfo)
    .on('click', '.revert-material-price-btn', RevertMaterialPrice)
    .on('click', '.delete-docs-btn', DeleteSelectedDocuments)
    .on('click', '.clear-contact-btn', ClearContactSelection)
    .on('click', '.edit-address-btn', LoadEditShippingAddress)
    .on('click', '.save-edit-address-btn', SaveEditShippingAddress);