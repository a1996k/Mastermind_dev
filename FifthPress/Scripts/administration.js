var gstDataTable;
var speedList;
var serviceSortable, helperSortable;
var serviceAdditionHelperTarget;

function AdminNav() {
    var toLoad = $(this).attr('data-ajax-load');
    $('.hidden-navver').val(toLoad);

    $.ajax({
        type: 'GET',
        url: ProcessUrl('AdminNav/' + toLoad),
        beforeSend: function () {
            $('.viewer-container').addClass('d-none');
            $('.admin-nav-group button').prop('disabled', true);
            if ($.fn.DataTable.isDataTable('#psDataTable')) {
                $('#psDataTable').DataTable().destroy();
                $('#psDataTable tbody').empty();
                $('#psDataTable thead').empty();
            }
            $('.viewer').html('');
            $('.sidebar-container').parent().hide();
            $('.sidebar-container').html('');
        },
        success: function (data) {
            var res = JSON.parse(data);
            if (typeof res.sideBar !== 'undefined') {
                $('.sidebar-container').html(res.sideBar);
                //$('.dynamic-button-home').html(res.createButtonHtml);
                $('#psDataTable thead').html(res.tableHeader);
                $('.viewer-container').removeClass('d-none');
                $('.sidebar-container').parent().show();
                $('.fc-table-content button').hide();
                $('.fc-table-content button[data-ajax-load="' + toLoad + '"]').show();
            } else {
                $('#psDataTable thead').html(res.tableHeader);
                $('#psDataTable .viewer').html(res.theView);
                $('#psDataTable').DataTable();
                //$('.dynamic-button-home').html(res.createButtonHtml);
                $('.viewer-container').removeClass('d-none');
                $('.fc-table-content button').hide();
                $('.fc-table-content button[data-ajax-load="' + toLoad + '"]').show();
            }
        },
        complete: function () {
            $('.admin-nav-group button').prop('disabled', false);
        }
    });
};

function AjaxNewService() {
    if (typeof serviceSortable !== 'undefined') {
        serviceSortable.sortable('destroy');
    }
    $.get(ProcessUrl('service/new'), function (data) {
        $('#edit_service_modal .modal-body').html(JSON.parse(data));
        ppSortable = $('.default-prepress-services').sortable();
        fSortable = $('.default-finishing-services').sortable();
        $('#editservicemodal').trigger('click');
    });
};
function AjaxNewMaterial() {
    $.get(ProcessUrl('material/new'), function (data) {
        $('#new_material_modal .modal-body').html(JSON.parse(data));
        $('#newmaterialmodal').trigger('click');
    });
};
function AjaxNewPress() {
    if (typeof fSortable !== 'undefined') {
        fSortable.sortable('destroy');
    }
    if (typeof ppSortable !== 'undefined') {
        ppSortable.sortable('destroy');
    }
    $.get(ProcessUrl('press/new'), function (data) {
        $('#new_press_modal .modal-body').html(JSON.parse(data));
        ppSortable = $('.default-prepress-services').sortable();
        fSortable = $('.default-finishing-services').sortable();
        $('#newpressmodal').trigger('click');
        $('#newpressmodal').trigger('click');
    });
};
function AjaxNewServiceCategory() {
    $.get(ProcessUrl('servicecat/new'), function (data) {
        $('#new_service_cat_modal .modal-body').html(JSON.parse(data));
        $('#newservicecatmodal').trigger('click');
    });
};
function AjaxNewUser() {
    $.get(ProcessUrl('user/new'), function (data) {
        $('#new_user_modal .modal-body').html(JSON.parse(data));
        $('#newusermodal').trigger('click');
    });
};
function AjaxNewClient() {
    $.get(ProcessUrl('client/new'), function (data) {
        $('#new_client_modal .modal-body').html(JSON.parse(data));
        $('#newclientmodal').trigger('click');

        if (typeof window.applyPermissions === 'function') {
            window.applyPermissions();
        }
    });
};
function AjaxEditClient() {
    var clientId = $(this).attr('data-record-id');

    $.get(ProcessUrl('client/edit/' + clientId), function (data) {
        $('#edit_client_modal .modal-body').html(JSON.parse(data));
        $('#editclientmodal').trigger('click');

        // Initialize client edit tabs after modal content is loaded
        if (typeof window.initializeClientEditTabs === 'function') {
            console.log('Calling initializeClientEditTabs from AjaxEditClient for client:', clientId);
            window.initializeClientEditTabs();
        }

        // Re-apply permissions so dynamically loaded elements (e.g. Tournament Budget tab) are shown/hidden correctly
        if (typeof window.applyPermissions === 'function') {
            window.applyPermissions();
        }
    });
};
function AjaxEditPlate() {
    var plateId = $(this).attr('data-record-id');

    $.get(ProcessUrl('plate/edit/' + plateId), function (data) {
        $('#edit_plate_modal .modal-body').html(JSON.parse(data));
        $('#editplatemodal').trigger('click');
    });
};
function AjaxNewPlate() {
    $.get(ProcessUrl('plate/new'), function (data) {
        $('#edit_plate_modal .modal-body').html(JSON.parse(data));
        $('#editplatemodal').trigger('click');
    });
};

function AjaxEditServiceCategory() {
    var $row = $(this);
    var r_id = $row.attr('data-record-id');
    var itemTitle = $row.attr('data-item-title');

    $('#edit_service_cat_modal h5.modal-title span').html(itemTitle);

    $.get(ProcessUrl('servicecat/edit/' + r_id), function (data) {
        $('#edit_service_cat_modal .modal-body').html(JSON.parse(data));
        $('#editservicecatmodal').trigger('click');
    });
};
function AjaxEditService() {
    var $row = $(this);
    var r_id = $row.attr('data-record-id');
    var itemTitle = $row.attr('data-item-title');

    $('#edit_service_modal h5.modal-title span').html(itemTitle);

    if (typeof fSortable !== 'undefined') {
        fSortable.sortable('destroy');
    }
    if (typeof ppSortable !== 'undefined') {
        ppSortable.sortable('destroy');
    }

    $.get(ProcessUrl('service/edit/' + r_id), function (data) {
        $('#edit_service_modal .modal-body').html(JSON.parse(data));
        ppSortable = $('.default-prepress-services').sortable();
        fSortable = $('.default-finishing-services').sortable();
        $('#editservicemodal').trigger('click');
    });
};

function AjaxEditMaterial() {
    var $row = $(this);
    var r_id = $row.attr('data-record-id');
    var itemTitle = $row.attr('data-item-title');

    $('#edit_material_modal h5.modal-title span').html(itemTitle);

    $.get(ProcessUrl('material/edit/' + r_id), function (data) {
        $('#edit_material_modal .modal-body').html(JSON.parse(data));
        $('#editmaterialmodal').trigger('click');
    });
};

function AjaxEditUser() {
    var $row = $(this);
    var r_id = $row.attr('data-record-id');
    var itemTitle = $row.attr('data-item-title');

    $('#edit_user_modal h5.modal-title span').html(itemTitle);

    $.get(ProcessUrl('user/edit/' + r_id), function (data) {
        $('#edit_user_modal .modal-body').html(JSON.parse(data));
        $('#editusermodal').trigger('click');
    });
};

function AjaxEditPress() {
    var $row = $(this);
    var r_id = $row.attr('data-record-id');
    var itemTitle = $row.attr('data-item-title');

    $('#edit_press_modal h5.modal-title span').html(itemTitle);

    if (typeof serviceSortable !== 'undefined') {
        serviceSortable.sortable('destroy');
    }

    $.get(ProcessUrl('press/edit/' + r_id), function (data) {
        $('#edit_press_modal .modal-body').html(JSON.parse(data));
        serviceSortable = $('.default-services').sortable();
        helperSortable = $('.helper-services').sortable();
        $('#editpressmodal').trigger('click');
    });
};

function GetPressHelperServices() {
    let arr = new Array();
    let pressId = $('#Press_Id').val();

    $('.helper-services li.service-entry').each(function () {
        let $ele = $(this);

        let s = {
            Id: $ele.attr('data-record-id'),
            Press: pressId,
            Service: $ele.attr('data-service-id')
        };

        arr.push(s);
    });

    return arr;
};
function GetDefaultPressServices() {
    let ctr = 1;
    let arr = new Array();
    let pressId = $('#Press_Id').val();
    let stockTypeId = $('.stock-type-group .active-choice').attr('data-record-id');
    $('.default-services li.service-entry').each(function () {
        let $ele = $(this);

        let s = {
            'Id': $ele.attr('data-record-id'),
            'Service': $ele.attr('data-service-id'),
            'Press': pressId,
            'Ordinal': ctr,
            'StockType': stockTypeId
        };

        arr.push(s);

        ctr++;
    });

    return arr;
};

function GetServiceFinishingServices() {
    if ($('.default-finishing-services li.service-entry').length > 0) {
        var arr = new Array();
        var serviceId = $('#Service_Id').val();

        let oTracker = 1;

        $('.default-finishing-services li.service-entry').each(function () {
            var s = {
                'Id': $(this).attr('data-record-id'),
                'LinkedService': $(this).attr('data-service-id'),
                'PrimaryService': serviceId,
                'Ordinal': oTracker
            };

            arr.push(s);

            oTracker++;
        });

        return arr;
    }
};

function GetServicePrepressServices() {
    if ($('.default-prepress-services li.service-entry').length > 0) {
        var arr = new Array();
        var serviceId = $('#Service_Id').val();

        let oTracker = 1;

        $('.default-prepress-services li.service-entry').each(function () {
            var s = {
                'Id': $(this).attr('data-record-id'),
                'LinkedService': $(this).attr('data-service-id'),
                'PrimaryService': serviceId,
                'Ordinal': oTracker
            };

            arr.push(s);

            oTracker++;
        });

        return arr;
    }
};

//TRYING OUT
function serializedToJson(serialized) {
    var json = {};
    var pairs = serialized.split('&');

    pairs.forEach(function (pair) {
        var parts = pair.split('=');
        var key = decodeURIComponent(parts[0]);
        var value = decodeURIComponent(parts[1]);

        // Remove the entity from the beginning of the key
        key = key.replace(/^Press\./, '');
        key = key.replace(/^Constraint\./, '');
        key = key.replace(/^Service\./, '');
        key = key.replace(/^User\./, '');
        key = key.replace(/^ServiceCategory\./, '');
        key = key.replace(/^Material\./, '');
        key = key.replace(/^PressSpeed\./, '');
        key = key.replace(/^Client\./, '');

        if (json[key] !== undefined) {
            if (!Array.isArray(json[key])) {
                json[key] = [json[key]];
            }
            json[key].push(value);
        } else {
            json[key] = value;
        }
    });

    return json;
};

function GetClientShippingAddresses() {
    let arr = [];
    $('.ship-address-list li').each(function () {
        let $item = $(this);
        let $address = {};

        $address.Id = $item.attr('data-record-id');
        $address.Client = $('#Client_Id').val();

        // Use more flexible selectors to handle both ID and name-based fields
        $address.Address = $item.find('input[id="Address"], input[name*="Address"]:not([name*="Address2"])').first().val();
        $address.Address2 = $item.find('input[id="Address2"], input[name*="Address2"]').first().val();
        $address.Attention = $item.find('input[id="Attention"], input[name*="Attention"]').first().val();
        $address.City = $item.find('input[id="City"], input[name*="City"]').first().val();
        $address.State = $item.find('select[id="State"], select[name*="State"]').first().val();
        $address.Phone = $item.find('input[id="Phone"], input[name*="Phone"]').first().val();
        $address.Email = $item.find('input[id="Email"], input[name*="Email"]').first().val();
        $address.Zip = $item.find('input[id="Zip"], input[name*="Zip"]').first().val();
        $address.Notes = $item.find('textarea[id="Notes"], textarea[name*="Notes"]').first().val();
        $address.IsDefaultForClient = $item.find('input[name="saRadio"]').is(':checked');
        $address.CustomerName = $item.find('input[id="CustomerName"], input[name*="CustomerName"]').first().val();

        arr.push($address);
    });

    return arr;
};

function GetClientContacts() {
    let arr = [];

    $('.contact-list li').each(function () {
        let $item = $(this);
        let $contact = {};

        $contact.Id = $item.attr('data-record-id');
        $contact.CusNo = $('#Client_Id').val();
        $contact.First = $item.find('#First').val();
        $contact.Last = $item.find('#Last').val();
        $contact.Phone = $item.find('#Phone').val();
        $contact.Fax = $item.find('#Fax').val();
        $contact.Email = $item.find('#Email').val();
        $contact.Position = $item.find('#Position').val();
        $contact.Options = $item.find('#Options').val();
        $contact.Phone2 = $item.find('#Phone2').val();
        $contact.Cell = $item.find('#Cell').val();
        $contact.KeyContact = $item.find('#KeyContact').val();
        $contact.ContactNote = $item.find('#ContactNote').val();
        $contact.ShipTo = $item.find('#ShipTo').val();
        $contact.ConTag = $item.find('#ConTag').val();

        arr.push($contact);
    });

    return arr;
};

function FormSubmitOverride(e) {
    e.preventDefault();
    var endpoint = $(e.target).attr('action');
    var props = $(this).serialize();

    //get the item type off of the form
    var itemType = $(e.target).attr('data-item-type');
    var itemTypeLower = itemType.toLowerCase();
    var theJson = serializedToJson(props);

    var dpsReturn = GetDefaultPressServices();
    var phsReturn = GetPressHelperServices();
    var sppsReturn = GetServicePrepressServices();
    var sfsReturn = GetServiceFinishingServices();

    if (itemTypeLower === 'press') {
        var savePress = {};
        savePress.theItem = theJson;
        savePress.DefaultPressServices = dpsReturn;
        savePress.HelperServices = phsReturn;
        savePress.StockType = $('.stock-type-group .active-choice').attr('data-record-id');

        $.ajax({
            type: 'POST',
            url: ProcessUrl(endpoint),
            data: {
                savePress: savePress
            }
        });
    }
    else if (itemTypeLower === 'service') {
        theJson.Required = $('#Service_Required').prop('checked');
        theJson.IsTimed = $('#Service_IsTimed').prop('checked');
        theJson.IsCuttingService = $('#Service_IsCuttingService').prop('checked');
        $.ajax({
            type: 'POST',
            url: ProcessUrl(endpoint),
            data: {
                theItem: theJson,
                FinishingServices: sfsReturn,
                PrepressServices: sppsReturn
            }
        });
    }
    else if (itemTypeLower === 'constraint') {
        //hacky but idc; note that this clears an existing maximum if changed and saved.
        if (theJson.ConstraintType !== '1') {
            theJson.MaxValue = $('#Constraint_MinValue').val();
        }

        $.ajax({
            type: 'POST',
            url: ProcessUrl(endpoint),
            data: {
                constraint: theJson,
                press: $('#Press_Id').val()
            },
            complete: GetPressConstraints
        });
    }
    else if (itemTypeLower === 'pressspeed') {
        theJson.Press = $('#Press_Id').val();
        theJson.IsDefault = $('#IsDefault_Value').val();

        $.ajax({
            type: 'POST',
            url: ProcessUrl(endpoint),
            data: {
                pressSpeed: theJson
            },
            complete: GetPressSpeeds
        });
    }
    else if (itemTypeLower === 'customer') {
        theJson.ShippingAddresses = GetClientShippingAddresses();
        theJson.Contacts = GetClientContacts();
        theJson.BlindShipping = $('#Client_BlindShipping_Value').is(':checked');
        theJson.TaxExempt = $('#Client_TaxExempt_Value').is(':checked');
        theJson.IsGolfClient = $('#Client_IsGolfClient_Value').is(':checked');
        theJson.CreditHold = $('#Client_CreditHold_Value').is(':checked');

        $.ajax({
            type: 'POST',
            url: ProcessUrl(endpoint),
            data: {
                theItem: theJson
            }
        });
    }
    else if (itemTypeLower === 'user') {
        // Use name selectors to match the hidden+checkbox pattern in GetUserForEdit.cshtml
        theJson.IsSpecialApprover = $('input[type="checkbox"][name="IsSpecialApprover"]').is(':checked');
        theJson.IsProductionEmployee = $('input[type="checkbox"][name="IsProductionEmployee"]').is(':checked');
        theJson.IsProjectManager = $('input[type="checkbox"][name="IsProjectManager"]').is(':checked');
        theJson.IsExecutive = $('input[type="checkbox"][name="IsExecutive"]').is(':checked');
        theJson.AgencyUtilizationApplicable = $('input[type="checkbox"][name="AgencyUtilizationApplicable"]').is(':checked');
        theJson.IsQAChecker = $('input[type="checkbox"][name="IsQAChecker"]').is(':checked');
        theJson.IsEventStaff = $('input[type="checkbox"][name="IsEventStaff"]').is(':checked');

        $.ajax({
            type: 'POST',
            url: ProcessUrl(endpoint),
            data: {
                theItem: theJson
            }
        });
    }
    else {
        $.ajax({
            type: 'POST',
            url: ProcessUrl(endpoint),
            data: {
                theItem: theJson
            }
        });
    }
};

function AddDefaultServices() {
    serviceAdditionHelperTarget = '.default-services';
};

function AddHelperServices() {
    serviceAdditionHelperTarget = '.helper-services';
};

function AddDefaultServicesToPress() {
    //get the following attributes from all #general_service_table tr recoreds that have a checked child
    // data-record-id and data-record-title
    var selectedServices = [];
    $('#general_service_table tr').each(function () {
        var $row = $(this);
        var $checkbox = $row.find('input[type="checkbox"]');
        if ($checkbox.is(':checked')) {
            selectedServices.push({
                id: $row.attr('data-record-id'),
                title: $row.attr('data-record-title')
            });
        }
    });

    var listSelector;

    listSelector = serviceAdditionHelperTarget;

    for (var i = 0; i < selectedServices.length; i++) {
        $(listSelector).prepend(
            `<li class="service-entry d-flex flex-row justify-content-between" data-record-id="0" data-service-id="${selectedServices[i].id}">
            <span class="title">${selectedServices[i].title}</span>
            <span class="remove-link">
                <i class="fa-solid fa-minus" style="color: #8f0000;"></i>
            </span>
        </li>`);
    }
};

function AddDefaultServicesToService() {
    var selectedServices = [];
    $('#general_service_table tr').each(function () {
        var $row = $(this);
        var $checkbox = $row.find('input[type="checkbox"]');
        if ($checkbox.is(':checked')) {
            selectedServices.push({
                id: $row.attr('data-record-id'),
                title: $row.attr('data-record-title')
            });
        }
    });

    var serviceType = $('#general_service_table tbody tr:first-child').attr('data-type-of-service');

    var listSelector;

    switch (serviceType) {
        case 'prepress':
            listSelector = '.default-prepress-services';
            break;
        case 'finishing':
            listSelector = '.default-finishing-services';
            break;
        default:
            listSelector = '';
    }

    for (var i = 0; i < selectedServices.length; i++) {
        $(listSelector).prepend(
            `<li class="service-entry d-flex flex-row justify-content-between" data-record-id="0" data-service-id="${selectedServices[i].id}">
            <span class="title">${selectedServices[i].title}</span>
            <span class="remove-link">
                <i class="fa-solid fa-minus" style="color: #8f0000;"></i>
            </span>
        </li>`);
    }
};

function GetConstraint() {
    let cId = $(this).parent().parent().attr('data-record-id');

    $('#constraint_list .modal-body').fadeOut('slow', function () {
        $.get(ProcessUrl('constraint/get/' + cId + '/1'), function (data) {
            $('#constraint_list .modal-body').html(JSON.parse(data));
            $('#constraint_list .modal-footer').fadeOut('slow', function () {
                $('#constraint_list .modal-body').fadeIn('fast');
            });
        });
    });
};

function CheckConstraintType() {
    $.get(ProcessUrl('ConstraintTypeCheck/' + $('#Constraint_ConstraintType').val()), function (data) {
        switch (data) {
            case 'value':
                $('.change-for-value').text('Value');
                $('.hide-for-value').hide();
                break;
            case 'range':
                $('.change-for-value').text('MinValue');
                $('.hide-for-value').show();
                break;
            default:
                //do nothing
                break;
        }
    });
};

function NewConstraint() {
    $('#constraint_list .modal-body').fadeOut('slow', function () {
        $.get(ProcessUrl('constraint/new'), function (data) {
            $('#constraint_list .modal-body').html(JSON.parse(data));
            $('#constraint_list .modal-footer').fadeOut('slow', function () {
                $('#constraint_list .modal-body').fadeIn('fast');
            });
        });
    });
};

function CancelConstraint() {
    $('#constraint_list').modal('hide');
    $('#vc').trigger('click');
};

function GetPressConstraints() {
    var pressId = $('#Press_Id').val();

    $('#constraint_list .modal-body').fadeOut('slow', function () {
        $.get(ProcessUrl('constraints/' + pressId), function (data) {
            $('#constraint_list .modal-body').html(JSON.parse(data));
            $('#constraint_list .modal-footer').fadeIn('fast', function () {
                $('#constraint_list .modal-body').fadeIn('fast');
            });
        });
    });
};

function SetClassOnButton(evt) {
    //check .hidden-navver and update the button as appropriate
    let hv = $('.hidden-navver').val();

    switch (hv) {
        case 'Services':
            $('.general-service-add').removeClass('general-service-add-presses').addClass('general-service-add-services');
            break;
        case 'Presses':
            $('.general-service-add').removeClass('general-service-add-services').addClass('general-service-add-presses');
            break;
        default:
            break;
    }
};

function GetPressSpeeds() {
    var pressId = $('#Press_Id').val();

    $('#speed_list .modal-body').fadeOut('slow', function () {
        $.get(ProcessUrl('get-press-speeds/' + pressId), function (data) {
            $('#speed_list .modal-body').html(data);
            $('#speed_list .modal-footer').fadeIn('fast', function () {
                $('#speed_list .modal-body').fadeIn('fast');
            });
        });
    });
};

function GetPressSpeed() {
    let cId = $(this).parent().parent().attr('data-record-id');

    $('#speed_list .modal-body').fadeOut('slow', function () {
        $.get(ProcessUrl('get-press-speed/' + cId), function (data) {
            $('#speed_list .modal-body').html(data);
            $('#speed_list .modal-footer').fadeOut('slow', function () {
                $('#speed_list .modal-body').fadeIn('fast');
            });
        });
    });
};

function NewPressSpeed() {
    $('#speed_list .modal-body').fadeOut('slow', function () {
        $.get(ProcessUrl('get-press-speed'), function (data) {
            $('#speed_list .modal-body').html(data);
            $('#speed_list .modal-footer').fadeOut('slow', function () {
                $('#speed_list .modal-body').fadeIn('fast');
            });
        });
    });
};

function SaveSpeedArrangement() {
    //build an array of speed id/ordinal values

    var items = $('.p-speeds li:not(:first-child)');
    var itemCt = items.length;
    var idArr = new Array();
    var ordinalArr = new Array();

    for (var i = 1; i <= itemCt; i++) {
        let item = items[(i - 1)];

        idArr.push($(item).attr('data-record-id'));
        ordinalArr.push(i);
    }

    var obj = {};
    obj.SpeedIds = idArr;
    obj.SpeedOrdinals = ordinalArr;

    $.ajax({
        type: 'POST',
        url: ProcessUrl('save-speed-arrangement'),
        data: {
            arrangement: obj
        },
        beforeSend: function () {
            $('body').addClass('pe-none');
        },
        complete: function () {
            $('body').removeClass('pe-none');
        }
    });
};

function ShowHideMinRunTime() {
    $('.min-run-time-block').is(':visible') ?
        $('.min-run-time-block').hide() :
        $('.min-run-time-block').show();
};

function LoadCategory() {
    var $cat = $(this);
    $cat.parent().find('.selected').removeClass('selected');
    $cat.addClass('selected');

    var catId = $cat.attr('data-record-id');
    var recordType = $cat.attr('data-record-type');
    //load cat items

    $.ajax({
        type: 'POST',
        url: ProcessUrl('admin/load-cat'),
        data: {
            id: catId,
            type: recordType
        },
        beforeSend: function () {
            $('.viewer-container').addClass('d-none');
            $('.viewer').html('');
            if ($.fn.DataTable.isDataTable('#psDataTable')) {
                $('#psDataTable').DataTable().destroy();
                $('#psDataTable tbody').empty();
            }
        },
        success: function (data) {
            try {
                var res = JSON.parse(data);
                $('.viewer').html(res.theView);
                $('#psDataTable thead').html(res.headerView);

                $('#psDataTable').DataTable({
                    pageLength: 100
                });
                $('.viewer-container').removeClass('d-none');

                $('.sidebar-wrapper').on('click', LoadCategory);
            } catch (e) { }
        }
    });
};

function ToggleStockType() {
    var $btn = $(this);
    var stockTypeId = $btn.attr('data-record-id');
    var pressId = $btn.parent().attr('data-press-id');

    $.ajax({
        type: 'GET',
        url: ProcessUrl('load-stock-config/' + stockTypeId + '/' + pressId),
        success: function (data) {
            var res = JSON.parse(data);

            if (res.HasRecord) {
                $('.edit-zone').html(res.Html);
                serviceSortable = $('.default-services').sortable();
                helperSortable = $('.helper-services').sortable();
            } else {
                $('#new_stock_type_modal').modal('show');
            }
        },
        complete: function () {
            $('.stock-type-group button.active-choice').removeClass('active-choice');
            $btn.addClass('active-choice');
        }
    });
};

function CancelStockType() {
    $('#new_stock_type_modal').modal('hide');
    $('button.sheet').trigger('click');
};

function NewStockType() {
    //ajax request the stock id and original press to the backend
    var origPress = $('#Press_Id').val();
    var stockId = $('.stock-type-group button.active-choice').attr('data-record-id');

    $.ajax({
        type: 'POST',
        url: ProcessUrl('new-stock-for-press'),
        data: {
            sourcePress: origPress,
            stockId: stockId
        },
        success: function (data) {
            console.log(data);
        },
        error: function (err) {
            console.log(err);
        },
        complete: function () {
            $('.stock-type-group button[data-record-id="' + stockId + '"]').trigger('click');
        }
    });
};

function RemoveContactFromClient() {
    let $row = $(this).parents('li');
    let rId = $row.attr('data-record-id');

    //ajax request the id to the backend and remove the contact record
    //remove the $row from the html

    if (confirm('Please confirm removing this contact')) {
        $.ajax({
            type: 'POST',
            url: ProcessUrl('remove-contact-from-client'),
            data: {
                id: rId
            },
            complete: function () {
                $row.remove();
            }
        });
    }
};

function AddContactToClient() {
    let clientId = $('#Client_Id').val();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('new-contact-for-client'),
        data: {
            clientId: clientId
        },
        success: function (data) {
            try {
                let res = JSON.parse(data);
                $('.contact-list').append(res);
            } catch (e) { }
        }
    });
};

function AddAddressToClient() {
    let clientId = $('#Client_Id').val();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('new-address-for-client'),
        data: {
            clientId: clientId
        },
        success: function (data) {
            try {
                let res = JSON.parse(data);
                $('.ship-address-list').append(res);
            } catch (e) { }
        }
    });
};

function AddFreightAddressToClient() {
    let clientId = $('#Client_Id').val();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('new-freight-address-for-client'),
        data: {
            clientId: clientId
        },
        success: function (data) {
            try {
                let res = JSON.parse(data);
                $('.freight-address-list').append(res);
            } catch (e) { }
        }
    });
};

function SaveTimeAdjustment() {
    let $btn = $(this);
    let $li = $btn.parents('li');
    let userId = $li.attr('data-user-id');
    let caId = $li.attr('data-ca-id');
    let ebId = $li.attr('data-eb-id');

    let saveObject = {
        UserId: userId,
        ClockActivityId: caId,
        BreakRecordId: ebId,
        ClockInTime: $li.find('.time-in').val(),
        ClockOutTime: $li.find('.time-out').val(),
        BreakStartTime: $li.find('.break-start').val(),
        BreakEndTime: $li.find('.break-end').val()
    };

    $.ajax({
        type: 'POST',
        url: ProcessUrl('save-clock-activity'),
        beforeSend: function () {
            $btn.prop('disabled', true);
        },
        data: {
            clockUpdate: saveObject
        },
        complete: function (data) {
            $btn.prop('disabled', false);
        }
    });
};

function LoadUserClockActivity() {
    let $btn = $(this);
    let userId = $btn.attr('data-record-id');

    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-clock-activity') + '/' + userId,
        beforeSend: function () {
            $('#clock_activity_edit_modal .modal-body ul').html('');
        },
        success: function (data) {
            //load data here
            try {
                let res = JSON.parse(data);

                for (let i = 0; i < res.length; i++) {
                    let thisRow =
                        `<li data-user-id="${res[i].UserId}" data-ca-id="${res[i].ClockActivityId}" data-eb-id="${res[i].EmployeeBreakId}">
                        <div class="container">
                            <div class="row">
                                <div class="col-lg-12">
                                    <div class="field-binder-row-flex">
                                        <div class="field-binder-col-flex fs-3">
                                            <span>${res[i].EventDateString}</span>
                                        </div>
                                        <div class="field-binder-col-flex ps-4">
                                            <label class="fs-4">Start Time</label>
                                            <input type="time" class="time-in" value="${res[i].TimeInString}" />
                                        </div>
                                        <div class="field-binder-col-flex ps-4">
                                            <label class="fs-4">Lunch Start</label>
                                            <input type="time" class="break-start" value="${res[i].BreakStartString}" />
                                        </div>
                                        <div class="field-binder-col-flex ps-4">
                                            <label class="fs-4">Lunch End</label>
                                            <input type="time" class="break-end" value="${res[i].BreakEndString}" />
                                        </div>
                                        <div class="field-binder-col-flex ps-4">
                                            <label class="fs-4">End Time</label>
                                            <input type="time" class="time-out" value="${res[i].TimeOutString}" />
                                        </div>
                                        <button type="button" class="btn btn-sm btn-fc-info save-this-row ms-3">Save</button>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </li>`;

                    $('#clock_activity_edit_modal .modal-body ul').append(thisRow);
                }
            } catch (e) { }
        }
    });
};

function DeleteShippingAddress() {
    var $btn = $(this);
    var $li = $btn.parents('li');
    var recordId = $btn.attr('data-record-id');

    if (confirm('Please confirm deleting this shipping address')) {
        $.ajax({
            type: 'POST',
            url: ProcessUrl('delete-shipping-address'),
            data: {
                recordId
            },
            success: function () {
                $li.remove();
            }
        });
    }
};

/**
 * Validate shipping address in existing row
 */
function ValidateShippingAddressRow() {
    var $btn = $(this);
    var $container = $btn.closest('li').find('.sa-container');
    var $feedback = $container.find('.address-validation-feedback');

    // Extract address fields from the row
    var address = $container.find('input[name*="Address"]:not([name*="Address2"])').val();
    var address2 = $container.find('input[name*="Address2"]').val();
    var city = $container.find('input[name*="City"]').val();
    var stateId = $container.find('select[name*="State"]').val();
    var zip = $container.find('input[name*="Zip"]').val();

    // Call validation function
    ValidateAddress($btn, $feedback, address, address2, city, stateId, zip);
}

/**
 * Validate shipping address in modal
 */
function ValidateShippingAddressModal() {
    var $btn = $(this);
    var $modal = $('#dynamic_sa_modal');
    var $feedback = $modal.find('.address-validation-feedback');

    // Extract address fields from modal
    var address = $modal.find('input[name="Address"]').val();
    var address2 = $modal.find('input[name="Address2"]').val();
    var city = $modal.find('input[name="City"]').val();
    var stateId = $modal.find('select[name="State"]').val();
    var zip = $modal.find('input[name="Zip"]').val();

    // Call validation function
    ValidateAddress($btn, $feedback, address, address2, city, stateId, zip);
}

/**
 * Core address validation logic using Avalara API
 */
function ValidateAddress($btn, $feedback, address, address2, city, stateId, zip) {
    // Show loading state
    var originalText = $btn.text();
    $btn.prop('disabled', true).text('Validating...');
    $feedback.hide();

    $.ajax({
        type: 'POST',
        url: '/Customers/ValidateShippingAddress',
        data: {
            address: address,
            address2: address2,
            city: city,
            stateId: stateId,
            zip: zip
        },
        success: function (response) {
            $btn.prop('disabled', false).text(originalText);

            // Check for suggestions first (regardless of whether address is valid)
            if (response.success && response.suggested && IsSuggestedDifferent(response.original, response.suggested)) {
                // Address has suggestions - show inline with apply button
                var suggestedHtml = '';

                // Show different header based on validity
                if (response.isValid) {
                    suggestedHtml += '<strong>✓ Address is Valid</strong><br>';
                    suggestedHtml += '<em>Avalara suggests a standardized format:</em><br><br>';
                } else {
                    suggestedHtml += '<strong>⚠ Address Suggestions</strong><br><br>';
                }

                suggestedHtml += '<div class="address-comparison">';
                suggestedHtml += '<div class="address-column">';
                suggestedHtml += '<strong>Your Address:</strong><br>';
                suggestedHtml += FormatAddressMultiline(response.original);
                suggestedHtml += '</div>';

                suggestedHtml += '<div class="address-column">';
                suggestedHtml += '<strong>Avalara Suggested:</strong><br>';
                suggestedHtml += FormatAddressMultiline(response.suggested);
                suggestedHtml += '</div>';
                suggestedHtml += '</div>';

                if (response.messages && response.messages.length > 0) {
                    suggestedHtml += '<br><strong>Improvements:</strong><ul>';
                    response.messages.forEach(function(msg) {
                        suggestedHtml += '<li>' + msg.summary + '</li>';
                    });
                    suggestedHtml += '</ul>';
                }

                suggestedHtml += '<div class="address-action-buttons">';
                suggestedHtml += '<button type="button" class="btn btn-fc-primary apply-suggested-address" style="margin-right: 10px;">Apply Suggested Address</button>';
                suggestedHtml += '<button type="button" class="btn btn-fc-caution keep-my-address">Keep My Address</button>';
                suggestedHtml += '</div>';

                // Use appropriate styling based on validity
                var bgColor = response.isValid ? '#d4edda' : '#fff3cd';
                var borderColor = response.isValid ? '#c3e6cb' : '#ffeaa7';
                var textColor = response.isValid ? '#155724' : '#856404';
                var cssClass = response.isValid ? 'address-valid' : 'address-warning';

                $feedback
                    .removeClass('address-error address-valid address-warning')
                    .addClass(cssClass)
                    .html(suggestedHtml)
                    .css({
                        'background-color': bgColor,
                        'border': '1px solid ' + borderColor,
                        'color': textColor
                    })
                    .data('suggested', response.suggested) // Store suggested address for later use
                    .data('container', $btn.closest('li, #dynamic_sa_modal')) // Store container reference
                    .show();
            } else if (response.success && response.isValid) {
                // Address is valid and no meaningful suggestions
                $feedback
                    .removeClass('address-error address-warning')
                    .addClass('address-valid')
                    .html('<strong>✓ Address is valid</strong>')
                    .css({
                        'background-color': '#d4edda',
                        'border': '1px solid #c3e6cb',
                        'color': '#155724'
                    })
                    .show();
            } else {
                // Validation failed
                var errorMsg = response.message || 'Address validation failed';
                $feedback
                    .removeClass('address-valid address-warning')
                    .addClass('address-error')
                    .html('<strong>✗ ' + errorMsg + '</strong>')
                    .css({
                        'background-color': '#f8d7da',
                        'border': '1px solid #f5c6cb',
                        'color': '#721c24'
                    })
                    .show();
            }
        },
        error: function (xhr, status, error) {
            $btn.prop('disabled', false).text(originalText);
            $feedback
                .removeClass('address-valid address-warning')
                .addClass('address-error')
                .html('<strong>✗ Validation failed:</strong> ' + error)
                .css({
                    'background-color': '#f8d7da',
                    'border': '1px solid #f5c6cb',
                    'color': '#721c24'
                })
                .show();
        }
    });
}

/**
 * Format address for display (single line)
 */
function FormatAddress(addr) {
    if (!addr) return 'N/A';
    var parts = [];
    if (addr.Line1) parts.push(addr.Line1);
    if (addr.Line2) parts.push(addr.Line2);
    if (addr.City) parts.push(addr.City);
    if (addr.Region) parts.push(addr.Region);
    if (addr.PostalCode) parts.push(addr.PostalCode);
    return parts.join(', ');
}

/**
 * Format address for display (multi-line for better readability)
 */
function FormatAddressMultiline(addr) {
    if (!addr) return 'N/A';
    var html = '';
    if (addr.Line1) html += addr.Line1 + '<br>';
    if (addr.Line2) html += addr.Line2 + '<br>';
    var cityStateZip = '';
    if (addr.City) cityStateZip += addr.City;
    if (addr.Region) cityStateZip += (cityStateZip ? ', ' : '') + addr.Region;
    if (addr.PostalCode) cityStateZip += (cityStateZip ? ' ' : '') + addr.PostalCode;
    if (cityStateZip) html += cityStateZip;
    return html || 'N/A';
}

/**
 * Check if suggested address is meaningfully different from original
 */
function IsSuggestedDifferent(original, suggested) {
    if (!original || !suggested) return false;

    // Normalize for comparison (lowercase, trim spaces)
    var normalize = function(str) {
        return (str || '').toLowerCase().trim().replace(/\s+/g, ' ');
    };

    // Compare each field
    var line1Different = normalize(original.Line1) !== normalize(suggested.Line1);
    var line2Different = normalize(original.Line2) !== normalize(suggested.Line2);
    var cityDifferent = normalize(original.City) !== normalize(suggested.City);
    var regionDifferent = normalize(original.Region) !== normalize(suggested.Region);
    var postalDifferent = normalize(original.PostalCode) !== normalize(suggested.PostalCode);

    // Return true if ANY field is different
    return line1Different || line2Different || cityDifferent || regionDifferent || postalDifferent;
}

/**
 * Apply suggested address from Avalara to form fields
 */
function ApplySuggestedAddress() {
    var $btn = $(this);
    var $feedback = $btn.closest('.address-validation-feedback');
    var suggested = $feedback.data('suggested');
    var $container = $feedback.data('container');

    if (!suggested || !$container) {
        alert('Unable to apply suggested address. Please try validating again.');
        return;
    }

    // Determine if we're in a modal or a row
    var isModal = $container.is('#dynamic_sa_modal');

    if (isModal) {
        // Apply to modal fields
        $container.find('input[name="Address"]').val(suggested.Line1 || '');
        $container.find('input[name="Address2"]').val(suggested.Line2 || '');
        $container.find('input[name="City"]').val(suggested.City || '');
        $container.find('input[name="Zip"]').val(suggested.PostalCode || '');

        // Find and set state dropdown by matching abbreviation
        if (suggested.Region) {
            var $stateSelect = $container.find('select[name="State"]');
            $stateSelect.find('option').each(function() {
                if ($(this).text().trim() === suggested.Region) {
                    $stateSelect.val($(this).val());
                    return false; // break
                }
            });
        }
    } else {
        // Apply to row fields - use same selectors as GetClientShippingAddresses
        var $saContainer = $container.find('.sa-container');
        $saContainer.find('input[id="Address"], input[name*="Address"]:not([name*="Address2"])').first().val(suggested.Line1 || '');
        $saContainer.find('input[id="Address2"], input[name*="Address2"]').first().val(suggested.Line2 || '');
        $saContainer.find('input[id="City"], input[name*="City"]').first().val(suggested.City || '');
        $saContainer.find('input[id="Zip"], input[name*="Zip"]').first().val(suggested.PostalCode || '');

        // Find and set state dropdown by matching abbreviation
        if (suggested.Region) {
            var $stateSelect = $saContainer.find('select[id="State"], select[name*="State"]').first();
            $stateSelect.find('option').each(function() {
                if ($(this).text().trim() === suggested.Region) {
                    $stateSelect.val($(this).val());
                    return false; // break
                }
            });
        }
    }

    // Update feedback to show success
    $feedback
        .removeClass('address-warning address-error')
        .addClass('address-valid')
        .html('<strong>✓ Suggested address applied!</strong><br>Form fields have been updated with Avalara\'s validated address.')
        .css({
            'background-color': '#d4edda',
            'border': '1px solid #c3e6cb',
            'color': '#155724'
        });
}

/**
 * Keep the user's original address (dismiss suggestions)
 */
function KeepMyAddress() {
    var $btn = $(this);
    var $feedback = $btn.closest('.address-validation-feedback');

    // Just hide the feedback
    $feedback
        .html('<strong>ℹ Your original address will be kept.</strong><br>You can save the customer with your current address.')
        .css({
            'background-color': '#d1ecf1',
            'border': '1px solid #bee5eb',
            'color': '#0c5460'
        })
        .delay(3000)
        .fadeOut();
}

function SetPassword() {
    let newPassword = $('.new-password-inp').val();
    let newPasswordConf = $('.new-password-conf-inp').val();

    if (newPassword === newPasswordConf) {
        $.ajax({
            type: 'POST',
            url: ProcessUrl('set-password'),
            data: {
                newPassword
            },
            success: function () {
                alert('Password updated');
            },
            error: function (err) {
                console.log(err);
                alert('Error changing password');
            }
        });
    } else {
        alert('Passwords do not match');
    }
};

function GoToBugs() {
    window.location = ProcessUrl('list-bugs');
};

function GoToDashboardConfigurator() {
    let userId = $(this).attr('data-user-id');
    if (confirm('This will take you to a different page. Any unsaved changes will be lost. Continue?')) {
        window.location = ProcessUrl('dashboard-configurator') + '/' + userId;
    }
};

$(function () { })
    .on('click', '.admin-nav-group button', AdminNav)
    .on('click', '.admin-edit-item-material', AjaxEditMaterial)
    .on('click', '.admin-edit-item-service', AjaxEditService)
    .on('click', '.admin-edit-item-press', AjaxEditPress)
    .on('click', '.admin-edit-item-servicecategory', AjaxEditServiceCategory)
    .on('click', '.admin-edit-item-user', AjaxEditUser)
    .on('click', '.admin-edit-item-plate', AjaxEditPlate)
    .on('click', '.admin-edit-item-customer', AjaxEditClient)
    .on('click', '.new-press-btn', AjaxNewPress)
    .on('click', '.new-material-btn', AjaxNewMaterial)
    .on('click', '.new-service-btn', AjaxNewService)
    .on('click', '.new-service-cat-btn', AjaxNewServiceCategory)
    .on('click', '.new-user-btn', AjaxNewUser)
    .on('click', '.new-plate-btn', AjaxNewPlate)
    .on('click', '.new-customer-btn', AjaxNewClient)
    .on('submit', 'form:not(#param_form)', FormSubmitOverride)
    .on('click', '.add-default-service', AddDefaultServices)
    .on('click', '.add-helper-service', AddHelperServices)
    .on('click', '.general-service-add-presses', AddDefaultServicesToPress)
    .on('click', '.general-service-add-services', AddDefaultServicesToService)
    .on('click', '.edit-constraint-link', GetConstraint)
    .on('click', '.new-constraint', NewConstraint)
    .on('click', '.cancel-constraint', CancelConstraint)
    .on('click', '.vc-wrapper', GetPressConstraints)
    .on('click', '.ps-wrapper', GetPressSpeeds)
    .on('click', '.edit-speed-link', GetPressSpeed)
    .on('change', '#Constraint_ConstraintType', CheckConstraintType)
    .on('show.bs.modal', '#general_service_modal', SetClassOnButton)
    .on('click', '.new-speed', NewPressSpeed)
    .on('sortstop', '.p-speeds', SaveSpeedArrangement)
    .on('change', '#Service_CatNo', ShowHideMinRunTime)
    .on('click', '.sidebar-wrapper li', LoadCategory)
    .on('click', '.stock-type-group button', ToggleStockType)
    .on('click', '.new-stock-type-btn', NewStockType)
    .on('click', '.cancel-new-stock-type', CancelStockType)
    .on('click', '.add-contact-btn', AddContactToClient)
    .on('click', '.remove-contact-btn', RemoveContactFromClient)
    .on('click', '.new-address-btn', AddAddressToClient)
    .on('click', '.new-freight-address-btn', AddFreightAddressToClient)
    .on('click', '.edit-clock-activity', LoadUserClockActivity)
    .on('click', '.save-this-row', SaveTimeAdjustment)
    .on('click', '.delete-sa-btn', DeleteShippingAddress)
    .on('click', '.validate-address-btn', ValidateShippingAddressRow)
    .on('click', '.validate-modal-address-btn', ValidateShippingAddressModal)
    .on('click', '.apply-suggested-address', ApplySuggestedAddress)
    .on('click', '.keep-my-address', KeepMyAddress)
    .on('click', '.submit-password', SetPassword)
    .on('click', '.bugs-btn', GoToBugs)
    .on('click', '.configure-dash-perm-btn', GoToDashboardConfigurator);