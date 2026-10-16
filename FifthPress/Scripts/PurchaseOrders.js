var myOrdersTable, awaitingTable;

var currentRowIdHelper = '';

function GetPOItems() {
    let arr = new Array();

    $('.po-item-wrapper .po-item-row').each(function () {
        let $ele = $(this);

        let obj = {
            Id: $ele.attr('data-record-id'),
            PurchaseOrder: $ele.find('input[name="Item.PurchaseOrder"]').val(),
            TotalPrice: $ele.find('input[name="Item.TotalPrice"]').val(),
            Job: $ele.find('input[name="Item.Job"]').val(),
            TrackInventory: $ele.find('input[name="Item.TrackInventory.Value"]').is(':checked'),
            DueDate: $ele.find('input[name="Item.DueDate"]').val(),
            Price: $ele.find('input[name="Item.Price"]').val(),
            Quantity: $ele.find('input[name="Item.Quantity"]').val(),
            CostType: $ele.find('select[name="Item.CostType"').val(),
            Notes: $ele.find('textarea[name="Item.Notes"]').val()
        };

        if (obj.TrackInventory === true) {
            obj.Item = $ele.find('input[name="Item.Item"]').val();
        } else {
            obj.ItemTitle = $ele.find('.item-autocomplete').val();
        }

        arr.push(obj);
    });

    return arr;
};

function GetVendorAddressInfo() {
    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-vendor-address-info') + '/' + $('#Order_Vendor').val(),
        success: function (data) {
            try {
                let d = JSON.parse(data);

                //address 1
                $('.vendor-addr-info .street-addr').html(d.Address);
                //address 2
                $('.vendor-addr-info .street-addr2').html(d.Address2);
                //city
                $('.vendor-addr-info .city-state-zip .city').html(d.City + ',');
                //state
                $('.vendor-addr-info .city-state-zip .state').html(d.State);
                //zip
                $('.vendor-addr-info .city-state-zip .zip').html(d.Zip);
                //phone
                $('.vendor-addr-info .phone').html(d.Phone);
                //email
                $('.vendor-addr-info .email').html(d.Email);

                //phone
                //$('.additional-vendor-info-row-one .phone-value').html(d.Phone);
                //tax no
                //$('.additional-vendor-info-row-one .tax-no-value').html(d.TaxNo);
                //Email
                //$('.additional-vendor-info-row-one .email-value').html(d.Email);
                //fax
                //$('.additional-vendor-info-row-two .fax-value').html(d.Fax);
            } catch (e) { }
        }
    });
};

function ValidatePurchaseOrder() {
    var vendor = $('#Order_Vendor').val();
    if (!vendor) {
        alert('Vendor is required. Please select a vendor before continuing.');
        return false;
    }
    return true;
}

function SavePurchaseOrder() {
    if (!ValidatePurchaseOrder()) return;

    let order = {};

    order.Id = $('#Order_Id').val();
    order.PONumber = $('#Order_PONumber').val();
    order.DateEntered = $('#Order_DateEntered').val();
    order.Vendor = $('#Order_Vendor').val();
    order.Status = $('#Order_Status').val();
    order.Requestor = $('#Order_Requestor').val();
    order.RequestDate = $('#Order_RequestDate').val();
    order.Freight = $('#Order_Freight').val();
    order.SubTotal = $('#Order_SubTotal').val();
    order.Tax = $('#Order_Tax').val();
    order.Total = $('#Order_Total').val();
    order.SubTotal = $('#Order_SubTotal').val();
    order.Paid = $('#Order_Paid').val();
    order.POTotal = $('.pototal-value').html().trim().replace('$', '');
    order.TaxPercent = $('#Order_TaxPercent').val();
    order.Notes = $('#Order_Notes').val();
    order.Terms = $('#Order_Terms').val();
    order.FOB = $('#Order_FOB').val();
    order.ShipVia = $('#Order_ShipVia').val();
    order.ShipToPhone = $('#Order_ShipToPhone').val();
    order.ShipToFax = $('#Order_ShipToFax').val();
    order.Emailed = $('#Order_Emailed').val();
    order.Received = $('#Order_Received').val();
    order.DateReceived = $('#Order_DateReceived').val();
    order.ShipTo = $('#Order_ShipTo').val();
    order.ShipToAddress = $('#Order_ShipToAddress').val();
    order.Price = $('#Order_Price').val();
    order.RequiredSpecialApproval = $('#Order_RequiredSpecialApproval').val();
    order.SpecialApprovalDate = $('#Order_SpecialApprovalDate').val();
    order.SpecialApprovalApprover = $('#Order_SpecialApprovalApprover').val();
    order.SpecialApprovalSent = $('#Order_SpecialApprovalSent.Value').val();
    order.SpecialApprovalSentDate = $('#Order_SpecialApprovalSentDate').val();
    order.CreatedBy = $('#Order_CreatedBy').val();
    order.PO_Items = GetPOItems();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('PurchaseOrder/Save'),
        data: {
            order
        },
        success: function (data) {
            alert('Order saved');
            try { let d = JSON.parse(data); } catch (e) { }
        }
    })
};

function NewPurchaseOrder() {
    window.location = ProcessUrl("PurchaseOrders/Create");
};

function SelectJob() {
    let rowId = $('#job_select_modal .po-item-id').val();
    let curJobNum = $('.po-item-row[data-record-id="' + rowId + '"] #Job').val();
    $('.job-num-autocomplete').val(curJobNum); //change
};
function CustomVendorAdditionSerialization(evt) {
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
                $('select[name="Order.Vendor"]').each(function () {
                    let $sel = $(this);
                    let curVal = $(this);
                    $sel.html(newSelectHtml);
                    $sel.val(curVal);
                });
            } catch (e) { }
        }
    });
};

function StoreRowId() {
    currentRowIdHelper = $(this).attr('data-row-id');
};

function AdjustTotalPrice() {
    let $ele = $(this);
    //called when qty, price or stock type changes

    //1 = PIECE
    //2 = HUNDRED
    //3 = THOUSAND
    //4 = FOOT

    let price = $ele.parents('.po-item-row').find('input[name="Item.Price"]').val();
    let costType = $ele.parents('.po-item-row').find('select[name="Item.CostType"]').val();
    let quantity = $ele.parents('.po-item-row').find('input[name="Item.Quantity"]').val();
    let newPrice = Number(quantity * price);

    if (costType === '2') {
        newPrice = newPrice / 100;
    }
    if (costType === '3') {
        newPrice = newPrice / 1000;
    }

    newPrice = newPrice.toFixed(2);

    $ele.parents('.po-item-row').find('.total-price-value').html('$' + newPrice);
    $ele.parents('.po-item-row').find('#Item_TotalPrice').val(newPrice);

    UpdateOverallPricing();
};

function GetTotalBeforeTax() {
    try {
        let freight = Number($('#Order_Freight').val());
        let itemTotal = 0;

        $('.po-item-row').each(function () {
            let $row = $(this);

            itemTotal += Number($row.find('#Item_TotalPrice').val());
        });

        let ttl = freight + itemTotal;
        $('.subtotal-value').html('$' + ttl.toFixed(2));

        return (freight + itemTotal);
    } catch (e) {
        alert('Double check entry values and ensure a cost type is selected for each item');
    }
};

function UpdateOverallPricing() {
    try {
        let totalBeforeTax = GetTotalBeforeTax();
        let overallPrice = roundToDecimalPlace(Number(totalBeforeTax) + Number($('#Order_Tax').val()), 2).toFixed(2);
        $('.pototal-value').html('$' + overallPrice);
    } catch (e) {
        alert('Double check entry values and ensure a cost type is selected for each item');
    }
};

function UpdateTaxDollars() {
    try {
        let taxPct = Number($('#Order_TaxPercent').val());
        let taxAmt = Number((taxPct / 100) * GetTotalBeforeTax());
        taxAmt = roundToDecimalPlace(taxAmt, 2).toFixed(2);
        $('#Order_Tax').val(taxAmt);
    } catch (e) { console.log('Error in UpdateTaxDollars()'); }

    AdjustTotalPrice(); //calling in here because I took the tax percent out of the blur for AdjustTotalPrice evt
};

function UpdateTaxPercent() {
    try {
        let taxAmt = Number($('#Order_Tax').val());
        let taxPct = Number(taxAmt / GetTotalBeforeTax());
        taxPct = taxPct * 100;
        taxPct = roundToDecimalPlace(taxPct, 2).toFixed(2);
        $('#Order_TaxPercent').val(taxPct);
    } catch (e) { console.log('Error in UpdateTaxPercent()'); }

    AdjustTotalPrice(); //calling in here because I took the tax percent out of the blur for AdjustTotalPrice evt
};

function RemoveRow() {
    if (confirm('Are you sure? This action cannot be undone')) {
        let $btn = $(this);
        let $mainContainer = $btn.parents('.po-item-row');
        let poId = $btn.attr('data-record-id');

        $.ajax({
            type: 'POST',
            url: ProcessUrl('remove-po-item'),
            data: {
                id: poId
            },
            success: function () {
                $mainContainer.remove();
                $('.add-po-row-btn').show();
            }
        });
    }
};

function AcceptDelivery() {
    //get item id and amount received
    //ajax to backend

    let itemId = $('#accept_delivery_modal .active-id').val();
    let quantityReceived = $('#accept_delivery_modal .amount-received').val();
    if (quantityReceived === '') {
        quantityReceived = 0;
    }

    $.ajax({
        type: 'POST',
        url: ProcessUrl('accept-delivery') + '/' + itemId + '/' + quantityReceived,
        success: function (data) {
            try {
                let res = JSON.parse(data);
            } catch (e) { }
        }
    })
};

function AddRow() {
    let poId = $(this).attr('data-po-id');

    $.ajax({
        type: 'GET',
        url: ProcessUrl('new-po-row') + '/' + poId,
        success: function (data) {
            try {
                let res = JSON.parse(data);
                $('.po-item-wrapper').append(res);

                $('.material-autocomplete:not(.ui-autocomplete)').autocomplete({
                    source: MaterialAutocompleteSearch,
                    select: MaterialAutocompleteSelect
                });
                $('.item-autocomplete:not(.ui-autocomplete)').autocomplete({
                    source: ItemAutocompleteSearch,
                    select: ItemAutocompleteSelect
                })
                $('.edit-btn')[$('.edit-btn').length - 1].click();
                $('.add-po-row-btn').hide();
            } catch (e) { }
        }
    });
};

function MaterialAutocompleteSearch(request, response) {
    $.ajax({
        url: ProcessUrl("AutocompleteMaterial"),
        type: 'GET',
        data: {
            'q': request.term
        },
        success: function (data) {
            let d = JSON.parse(data);

            response($.map(d, function (item) {
                return { label: item.Title, value: item.Title, recordId: item.Id, friendlyName: item.FriendlyName }
            }));
        }
    });
};

function MaterialAutocompleteSelect(event, ui) {
    let selector = '.po-item-row #material_' + currentRowIdHelper;
    $(selector).val(ui.item.recordId);
};

function ItemAutocompleteSearch(request, response) {
    $.ajax({
        type: 'GET',
        url: ProcessUrl('AutocompleteItem'),
        data: {
            q: request.term
        },
        success: function (data) {
            let d = JSON.parse(data);

            response($.map(d, function (item) {
                return { label: item.Title, value: item.Title, recordId: item.Id }
            }));
        }
    });
};

function ItemAutocompleteSelect(event, ui) {
    let selector = '.po-item-row #material_' + currentRowIdHelper;
    $(selector).val(ui.item.recordId);
};

function PrepAcceptModal() {
    let $btn = $(this);
    let rId = $btn.attr('data-record-id');

    $('#accept_delivery_modal .active-id').val(rId);

    //ajax request to get remaining delivery amount
    let rowId = $('#accept_delivery_modal .active-id').val();

    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-delivery-status') + '/' + rowId,
        success: function (data) {
            try {
                let res = JSON.parse(data);

                //clear the history list
                $('#accept_delivery_modal .delivery-list').html('');

                for (var i = 0; i < res.orders.length; i++) {
                    let raDate = res.orders[i].ReceivedAt.split('T')[0];

                    let newHtml = `<li>Quantity {${res.orders[i].AmountReceived}} received by ${res.orders[i].receivedBy} on ${raDate}</li>`;
                    $('#accept_delivery_modal .delivery-list').append(newHtml);
                }

                $('#accept_delivery_modal .amount-received').attr('max', res.remainingQty);
                $('#accept_delivery_modal .amount-remaining').html(res.remainingQty);
                $('#accept_delivery_modal .item-title').html(res.materialName);
                $('#accept_delivery_modal .amount-received').val('');
            } catch (e) { console.log(e); }
        }
    });
};

function PrepDeliveryHistoryModal() {
    let $btn = $(this);
    let rId = $btn.attr('data-record-id');

    $('#delivery_history_modal .active-id').val(rId);

    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-delivery-status') + '/' + $('#delivery_history_modal .active-id').val(),
        success: function (data) {
            try {
                let res = JSON.parse(data);

                //clear the history list
                $('#delivery_history_modal .delivery-list').html('');

                for (var i = 0; i < res.orders.length; i++) {
                    let raDate = res.orders[i].ReceivedAt.split('T')[0];

                    let newHtml = `<li>Quantity {${res.orders[i].AmountReceived}} received by ${res.orders[i].receivedBy} on ${raDate}</li>`;
                    $('#delivery_history_modal .delivery-list').append(newHtml);
                }

                $('#delivery_history_modal .item-title').html(res.materialName);
            } catch (e) { console.log(e); }
        }
    });
};

function PrepInvoiceModal() {
    let $btn = $(this);
    let rId = $btn.attr('data-record-id');

    $('#invoice_modal input[name="ItemId"]').val(rId);
};

function JobNumAutocompleteSearch(request, response) {
    $.ajax({
        url: ProcessUrl("AutocompleteJobNumber"),
        type: 'GET',
        data: {
            'q': request.term
        },
        success: function (data) {
            let d = JSON.parse(data);

            response($.map(d, function (item) {
                return { label: item.JobNumber, value: item.JobNumber, recordId: item.Id }
            }));
        }
    });
};


//need to debug this function (not setting job)
function JobNumberAutocompleteSelect(event, ui) {
    console.log('JobNumberAutocompleteSelect()');
    let rowId = $('#job_select_modal .po-item-id').val();
    let rowSelector = '.po-item-row[data-record-id="' + rowId + '"]';

    $(rowSelector).find('#Item_Job').val(ui.item.recordId);
    $(rowSelector).find('.job-link').attr('href', ('/Job/Edit/' + ui.item.recordId));
    $(rowSelector).find('.change-job-button').html(ui.item.label);
    $(rowSelector).find('.job-link').show();
};

function ApprovePurchaseOrder() {
    let poId = $(this).attr('data-record-id');

    $.ajax({
        type: 'POST',
        url: ProcessUrl('approve-po'),
        data: {
            poId
        },
        success: function () {
            $('.approve-po-btn').remove();
            $('.deny-po-btn').remove();
            $('.approved-text').removeClass('d-none');
            $('.awaiting-approval-btn').remove();
            $('.approved-text').removeClass('d-none');

            window.location.reload();
        },
        error: function (err) {
            alert('Error approving purchase order. Please ensure you have the proper permissions and are not the requester.')
        }
    });
};

function DenyPurchaseOrder() {
    let poId = $('.purchase-order-id').val();
    let reason = $('#denial_reason_modal #denial_reason').val();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('deny-po'),
        data: {
            poId,
            reason
        },
        success: function () {
            $('.awaiting-approval-btn').remove();
            $('.denied-text').removeClass('d-none');
            window.location.reload();
        }
    });
};

function PrepJobModal() {
    let rId = $(this).attr('data-record-id');
    $('#job_select_modal .po-item-id').val(rId);
    $('.job-num-autocomplete').val('');
};

function AutocompleteSwitcher() {
    let $cb = $(this);
    //let $acInput = $cb.parents('po-item-row').find('.material-autocomplete');
    let $row = $cb.parents('.po-item-row');

    if ($cb.is(':checked')) {
        //material autocomplete
        $row.find('.item-autocomplete').hide();
        $row.find('.material-autocomplete').show();
    } else {
        //item autocomplete
        $row.find('.material-autocomplete').hide();
        $row.find('.item-autocomplete').show();
    }
};

function CollapseChildRows() {
    let pId = $(this).closest('tr').attr('data-partner-id');
    let $table = $(this).closest('table');
    let $collapser = $(this);
    let $expander = $(this).parent().find('.expand-row-btn');

    $table.find('tr:not(.parent-row)[data-partner-id="' + pId + '"]').fadeOut('fast');
    $collapser.hide();
    $expander.show();
};

function ExpandChildRows() {
    let pId = $(this).closest('tr').attr('data-partner-id');
    let $table = $(this).closest('table');
    let $collapser = $(this).parent().find('.collapse-row-btn');
    let $expander = $(this);

    $table.find('tr:not(.parent-row)[data-partner-id="' + pId + '"]').fadeIn('fast');
    $expander.hide();
    $collapser.show();
};

function CancelPurchaseOrder() {
    if (confirm('Are you sure you want to cancel this purchase order?')) {
        let $btn = $(this);
        let recId = $btn.attr('data-record-id');

        $.ajax({
            type: 'POST',
            url: ProcessUrl('cancel-po'),
            data: {
                id: recId
            },
            success: function (data) {
                alert('Purchase Order Cancelled');
                $('.cancel-po-btn').remove();
                $('.cancelled-text').removeClass('d-none');

                window.location.reload();
            }
        });
    }
};

function SubmitForApproval() {
    if (!ValidatePurchaseOrder()) return;

    if (confirm('Are you sure you want to submit this order for approval? You will not be able to modify the order until it is either approved or denied')) {
        let poid = $('#Order_Id').val();

        $.ajax({
            type: 'POST',
            url: ProcessUrl('submit-po-for-approval'),
            data: {
                id: poid
            },
            success: function (data) {
                alert('Order submitted for approval');
                //disable save button and change text to "awaiting approval"
                $('.save-btn').html('Awaiting Approval').removeClass('save-btn').attr('disabled', true);
                //remove button for approval
                $('.submit-for-app-btn').remove();

                window.location.reload();
            }
        });
    }
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

function DisableInputs() {
    $('.po-item-row input, .po-item-row button:not(.po-action-btn), .po-hdr-ctr input:not(#Order_Freight,#Order_Tax,#Order_TaxPercent), .po-hdr-ctr select, .po-hdr-ctr button:not(.po-action-btn,.print-btn)').prop('disabled', true);
};

function PrintablePurchaseOrder() {
    if (!ValidatePurchaseOrder()) return;

    let oId = $('#Order_Id').val();

    window.open(ProcessUrl('PurchaseOrder/Print/') + oId, '_blank');
};

$(function () {
    if (purchaseOrderApproved || purchaseOrderPending) {
        DisableInputs();
    }
    myOrdersTable = $('#my_orders_table').DataTable({
        searching: "false",
        sorting: "false"
    });
    awaitingTable = $('#awaiting_approval_table').DataTable({
        searching: "false",
        sorting: "false"
    });

    $('.job-num-autocomplete').autocomplete({
        source: JobNumAutocompleteSearch,
        select: JobNumberAutocompleteSelect
    });

    $('.material-autocomplete').autocomplete({
        source: MaterialAutocompleteSearch,
        select: MaterialAutocompleteSelect
    });

    $('.item-autocomplete').autocomplete({
        source: ItemAutocompleteSearch,
        select: ItemAutocompleteSelect
    });

    //disable item inputs
    //$('.po-item-row input,.po-item-row select,.po-item-row input[type="checkbox"],.po-item-row .change-job-btn').prop('disabled', true);

    $('.sortable-row').sortable();
    UpdateOverallPricing();
})
    .on('click', '.new-po', NewPurchaseOrder)
    .on('click', '.save-dynamic-vendor', CustomVendorAdditionSerialization)
    .on('show.bs.modal', '#job_select_modal', SelectJob)
    .on('click', '.save-btn', SavePurchaseOrder)
    .on('change', '#Order_Vendor', GetVendorAddressInfo)
    .on('focus', '.material-autocomplete,.item-autocomplete', StoreRowId)
    .on('blur', 'input[name="Item.Price"],select[name="Item.CostType"],input[name="Item.Quantity"],input[name="Order.Freight"],input[name="Order.Tax"],input[name="Order.TaxPercent"]', AdjustTotalPrice)
    .on('click', '.remove-btn', RemoveRow)
    .on('click', '.submit-delivery-btn', AcceptDelivery)
    .on('click', '.accept-delivery-btn', PrepAcceptModal)
    .on('click', '.delivery-history-btn', PrepDeliveryHistoryModal)
    .on('click', '.add-po-row-btn', AddRow)
    .on('click', '.approve-po-btn', ApprovePurchaseOrder)
    .on('click', '.submit-denial-btn', DenyPurchaseOrder)
    .on('blur', 'input[name="Order.TaxPercent"]', UpdateTaxDollars)
    .on('blur', 'input[name="Order.Tax"]', UpdateTaxPercent)
    /*.on('blur', '#Order_Freight')*/
    .on('click', '.change-job-btn', PrepJobModal)
    .on('click', '.invoice-btn', PrepInvoiceModal)
    .on('click', '#Item_TrackInventory_Value', AutocompleteSwitcher)
    .on('click', '.collapse-row-btn', CollapseChildRows)
    .on('click', '.expand-row-btn', ExpandChildRows)
    .on('click', '.cancel-po-btn', CancelPurchaseOrder)
    .on('click', '.submit-for-app-btn', SubmitForApproval)
    .on('click', '.save-dynamic-material', CustomMaterialAdditionSerialization)
    .on('click', '.print-btn', PrintablePurchaseOrder);