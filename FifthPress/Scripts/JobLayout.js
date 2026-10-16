var layouts = [];
var swappedLayouts = [];
var lastSheets = [];
var isSwappedLayout = [];
var isMixedGrain = [];
var carousel;
var curCarIndex = 0;

function BackToEdit() {
    //go back to the edit screen
    //var recId = $('#Job_Id').val();
    //window.location = ProcessUrl('Job/Edit/' + recId);
    $('#layout_modal').modal('hide');
};

function Save() {
    let rowId = $('#sp_adder_helper').val();
    let $row = $('.job-item-row[data-record-id="' + rowId + '"]');
    let config = {};
    let counter = 0;

    let $layout = $('#layout_modal');

    config = {
        "JobItemId": $layout.find('.ji-id').val(),
        "Quantity": $layout.find('input[name="quantity"]').val(),
        "Up": $layout.find('input[name="up"').val(),
        "MarginXL": $layout.find('input[name="MarginXL"]').val(),
        "MarginXR": $layout.find('input[name="MarginXR"]').val(),
        "MarginYT": $layout.find('input[name="MarginYT"]').val(),
        "MarginYB": $layout.find('input[name="MarginYB"]').val(),
        "SideGuides": $layout.find('input[name="SideGuides"]').val(),
        "Gripper": $layout.find('input[name="Gripper"]').val(),
        "Tail": $layout.find('input[name="Tail"]').val(),
        "UpValueLocked": $layout.find('.up-val-locked').val()
    };

    $.ajax({
        type: 'POST',
        url: ProcessUrl('override-job-layout'),
        data: {
            overrides: config
        },
        success: function (data) {
            $('.back-btn').trigger('click');
            $('.run-calcs').trigger('click');
            $row.find('.calced-up-val').val(config.Up);
        }
    });
};

function SwapLayout() {
    let ind = carousel.data('owlCarousel').currentItem;

    $('#material_' + ind).empty();

    let ll;

    if (isSwappedLayout[ind]) {
        ll = layouts[ind];
    } else {
        ll = swappedLayouts[ind];
    }

    isSwappedLayout[ind] = !isSwappedLayout[ind];

    let inpIndexStr = ind;

    ll.Quantity = $('#qty_material_' + inpIndexStr).val();
    ll.Up = $('#up_material_' + inpIndexStr).val();
    ll.MarginXL = $('#marginxl_material_' + inpIndexStr).val();
    ll.MarginXR = $('#marginxr_material_' + inpIndexStr).val();
    ll.MarginYT = $('#marginyt_material_' + inpIndexStr).val();
    ll.MarginYB = $('#marginyb_material_' + inpIndexStr).val();

    new LayoutManager(new Layout(ll));
};

function UpdateModel(submittedId) {
    let $inp = $(this);
    let inpIndexStr;
    try {
        inpIndexStr = $inp.attr('id').split('_')[2];
    } catch (e) {
        //get from parameter
        inpIndexStr = submittedId.split('_')[2];
    }

    let maxUpValue = $('#up_material_0').attr('data-max-up');
    let thisUpValue = $('#up_material_0').val();

    $('#material_0').empty();

    let inpIndex = Number(inpIndexStr);
    let config = isSwappedLayout[inpIndex] ? swappedLayouts[inpIndex] : layouts[inpIndex];
    //don't need to swap because it's not a layout change ^^^

    config.Quantity = $('#qty_material_0').val();
    config.Up = $('#up_material_0').val();
    config.MarginXL = $('#marginxl_material_0').val();
    config.MarginXR = $('#marginxr_material_0').val();
    config.MarginYT = $('#marginyt_material_0').val();
    config.MarginYB = $('#marginyb_material_0').val();
    config.SideGuides = $('#sideGuides_material_0').val();
    config.Gripper = $('#gripper_material_0').val();
    config.Tail = $('#tail_material_0').val();

    //config.ShowLast = onLast;

    if (thisUpValue <= maxUpValue) {
        $('#material_0').show();
        $('#custom_material_0').hide();
        new LayoutManager(new Layout(config));
    } else {
        SwitchToCustomLayout(inpIndexStr);
    }
};

function SwitchToCustomLayout(slideIndex) {
    $('#material_' + slideIndex).hide();
    $('#custom_material_' + slideIndex).show();
};

function UpdateMixGrain() {
    let ind = carousel.data('owlCarousel').currentItem;
    isMixedGrain[ind] = !isMixedGrain[ind];
    $('#mix_grain').prop('checked', isMixedGrain[ind]);
};
/*
function ToggleFirst() {
    $('#material_' + curCarIndex).empty();
    let inpIndex = Number(curCarIndex);
    let config = isSwappedLayout[inpIndex] ? swappedLayouts[inpIndex] : layouts[inpIndex];

    config.Up = Number($('#up_material_' + inpIndex).val());
    config.Quantity = Number($('#qty_material_' + inpIndex).val());

    config.ShowLast = false;
    onLast = false;
    new LayoutManager(
        new Layout(config)
    );

    $('#material_' + inpIndex).parent().parent().parent().children('.first-last-toggle').css('visibility', 'visible');
};

function ToggleLast() {
    $('#material_' + curCarIndex).empty();
    let inpIndex = Number(curCarIndex);
    let config = isSwappedLayout[inpIndex] ? swappedLayouts[inpIndex] : layouts[inpIndex];

    config.Up = Number($('#up_material_' + inpIndex).val());
    config.Quantity = Number($('#qty_material_' + inpIndex).val());
    //don't need to swap because it's not a layout change ^^^

    config.ShowLast = true;
    onLast = true;
    new LayoutManager(
        new Layout(config)
    );

    $('#material_' + inpIndex).parent().parent().parent().children('.first-last-toggle').css('visibility', 'visible');
};

function ToggleFirstLast() {
    var $ele = $(this);
    var dId = $ele.attr('data-material-id');
    var flAction = $ele.attr('data-fl-action');

    switch (flAction) {
        case 'l':
            ToggleLast(dId);
            break;
        case 'f':
            ToggleFirst(dId);
            break;
        default:
            break;
    }
};*/

//will need update due to new margin updates
function OnOffLinker() {
    let $cb = $(this);
    let $x = $($cb.attr('data-margin-x-id'));
    let $y = $($cb.attr('data-margin-y-id'));

    if ($cb.is(':checked')) {
        $y.prop('disabled', true);

        $x.on('change', function () {
            $y.val($x.val());
        });

        $x.trigger('focus');
    } else {
        $x.off('change');

        $y.prop('disabled', false);
    }
};

function MaxUp() {
    var $upL = $(this);
    var $upInp = $($upL.attr('data-input-partner'));
    $upInp.val($upInp.attr('max'));
};

function LoadPressConfig() {
    var recordId = $(this).attr('data-record-id');

    $.ajax({
        type: 'GET',
        url: 'QuoteItemPress/Edit/' + recordId,
        success: function (data) {
            var $loadDest = $('#edit_press_modal .modal-body');
            $loadDest.html(JSON.parse(data.responseJSON));
        }
    });

    $.ajax({
        type: 'GET',
        url: ProcessUrl('QuoteItemService/Edit/' + serRecId),
        complete: function (data) {
            //data should be the html for the service edit, so load it
            var $loadDest = $('#edit_service_modal .modal-body');
            $loadDest.html(JSON.parse(data.responseJSON));

            //then click the button
            var btnId = '#editservice';

            $(btnId).trigger('click');
        }
    });
};

function UpValueChange() {
    let $inp = $(this);
    let rId = $('#sp_adder_helper').val();
    let $row = $('.job-item-row[data-record-id="' + rId + '"]');
    $row.find('.up-val-locked').val('True');
    UpdateModel($inp.attr('id'));
};

$(function () {

    $('.service-order-list').sortable();

    $('.swap-btn').on('click', SwapLayout);
})
    .on('click', '.back-btn', BackToEdit)
    .on('click', '.save-btn', Save)
    .on('change', '#layout_modal input[name="quantity"]', UpdateModel)
    .on('change', '#layout_modal input[name="up"]', UpValueChange)
    .on('click', '#mix_grain', UpdateModel)
    .on('change', '#layout_modal input[name="MarginXR"],#layout_modal input[name="MarginXL"]', UpdateModel)
    .on('change', '#layout_modal input[name="MarginYT"],#layout_modal input[name="MarginYB"]', UpdateModel)
    .on('change', '#layout_modal input[name="SideGuides"]', UpdateModel)
    .on('change', '#layout_modal input[name="Gripper"]', UpdateModel)
    .on('change', '#layout_modal input[name="Tail"]', UpdateModel)
    //.on('click', 'input[type="radio"]', ToggleFirstLast)
    .on('click', '.config-press-btn', LoadPressConfig);