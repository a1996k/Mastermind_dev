function LoadShippedOrders() {
    let $modal = $('#shipped_orders_modal');
    $modal.find('.modal-body .dynamic-load-tgt').html('');
    $modal.find('.modal-header').html('');
    $modal.find('.modal-footer').html('');
    //ajax get for model
    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-shipped-orders') + '/' + $('#cur_jid').val(),
        success: function (data) {
            try {
                let res = JSON.parse(data);
                $modal.find('.modal-header').html(res.ModalHeader);
                $modal.find('.modal-body .dynamic-load-tgt').html(res.ModalBody);
                $modal.find('.modal-footer').html(res.ModalFooter);
            } catch (e) { }
        },
        error: function () {
            alert('Error getting shipped orders');
        }
    });
};

function CalculateFreightPrice() {
    let dollarAmount = parseFloat($('#shipped_orders_modal .order-freight-amt').val());
    let markup = parseFloat($('#shipped_orders_modal .order-markup-val').val());

    let price = 0;

    // Check if the inputs are valid numbers
    if (isNaN(dollarAmount) || isNaN(markup)) {
        price = 0; //just so it's not empty in here
    } else {
        // Calculate the price
        price = dollarAmount + (dollarAmount * markup);
    }

    // Display the calculated price
    $('#shipped_orders_modal .order-freight-price').val(price.toFixed(2));
};

function ValidateShipmentForm() {
    // Get the values from the inputs
    let freightAmount = $('#shipped_orders_modal .order-freight-amt').val();
    let markup = $('#shipped_orders_modal .order-markup-val').val();
    let freightPrice = $('#shipped_orders_modal .order-freight-price').val();
    let trackingNumber = $('#shipped_orders_modal .order-tracking-num').val();

    // Validate Freight Amount (must be a decimal number)
    //console.log('freight cost = ' + freightAmount);
    if (!/^\d+(\.\d{1,2})?$/.test(freightAmount)) {
        alert('Freight Amount must be a valid decimal number (e.g., 25.00).');
        return false;
    } else {
        //console.log('Passed');
    }

    // Validate Markup (must be a decimal number)
    //console.log('markup = ' + markup);
    if (!/^\d*(\.\d+)?$/.test(markup)) {
        alert('Markup must be a valid decimal number (e.g., 0.40 for 40%).');
        return false;
    } else {
        //console.log('Passed');
    }

    // Validate Freight Price (must be a decimal number)
    //console.log('freight price = ' + freightPrice);
    if (!/^\d+(\.\d{1,2})?$/.test(freightPrice)) {
        alert('Freight Price must be a valid decimal number (e.g., 25.00).');
        return false;
    } else {
        //console.log('Passed');
    }

    // Validate Tracking Number (must contain text)
    if (trackingNumber.trim() === '') {
        alert('Tracking Number must contain text.');
        return false;
    } else {
        //console.log('Passed');
    }

    // Notes is optional, no validation needed
    // Internal Charge can be checked or unchecked, no validation needed

    // If all validations pass
    return true;
};

function GetShipmentModel() {
    let model = {};

    model.Job = $('#cur_jid').val();
    model.FreightCost = $('#shipped_orders_modal .order-freight-amt').val();
    model.Markup = $('#shipped_orders_modal .order-markup-val').val();
    model.FreightPrice = $('#shipped_orders_modal .order-freight-price').val();
    model.TrackingNumber = $('#shipped_orders_modal .order-tracking-num').val();
    model.Notes = $('#shipped_orders_modal .order-notes').val();
    model.InternalCharge = $('#shipped_orders_modal .order-internal-charge').is(':checked');

    return model;
};

function SubmitShipment() {
    //if form is valid
    if (ValidateShipmentForm()) {
        //get the shipment model
        let model = GetShipmentModel();
        //ajax to backend
        $.ajax({
            type: 'POST',
            url: ProcessUrl('save-order-shipment'),
            data: {
                model
            },
            success: function (data) {
                //update UI

                //clear fields
                $('#shipped_orders_modal .order-freight-amt').val('');
                $('#shipped_orders_modal .order-markup-val').val('.40');
                $('#shipped_orders_modal .order-freight-price').val('');
                $('#shipped_orders_modal .order-tracking-num').val('');
                $('#shipped_orders_modal .order-notes').val('');
                $('#shipped_orders_modal .order-internal-charge').prop('checked', false);

                LoadShippedOrders();
            },
            error: function () {
                alert('Error saving shipment; contact a developer');
            }
        });
    }
};

$(function () { })
    .on('show.bs.modal', '#shipped_orders_modal', LoadShippedOrders)
    .on('blur', '#shipped_orders_modal .order-markup-val', CalculateFreightPrice)
    .on('click', '.add-shipment-btn', SubmitShipment);