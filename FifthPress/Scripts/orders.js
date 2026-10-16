function ShowOtherMaterialEntryField() {
    $('.other-mat-entry').show();
};
function HideOtherMaterialEntryField() {
    $('.other-mat-entry').hide();
    $('.other-mat-entry').val('');
};
function OtherMaterialEntryCheck() {
    let $l = $(this);
    let $sel = $l.find('option:selected');

    //ensure something is selected
    if ($sel) {
        //show
        if ($sel.attr('value') == '99999') {
            ShowOtherMaterialEntryField();
        }
        //hide
        else {
            HideOtherMaterialEntryField();
        }
    }
};

function SubmitRequest() {
    let model = GetFormModel();
    let formData = new FormData();

    // Append request properties
    for (const key in model.request) {
        formData.append(`request.${key}`, model.request[key]);
    }

    // Append items
    model.items.forEach((item, index) => {
        for (const key in item) {
            formData.append(`items[${index}].${key}`, item[key]);
        }
    });

    // Append files
    let files = $('.files')[0].files; // Get actual file objects
    for (let i = 0; i < files.length; i++) {
        formData.append("files", files[i]); // Append actual file, not metadata
    }

    $.ajax({
        type: 'POST',
        url: ProcessUrl('submit-order-request'),
        data: formData,
        processData: false,  // Prevent jQuery from processing the data
        contentType: false,  // Ensure proper file upload encoding
        success: function (data) {
            alert('Form submitted successfully!');
            window.location.reload();
        },
        error: function (err) {
            alert('Error during submission.');
        }
    });
};


function GetFormModel() {

    let model = {};

    model.request = {
        Client: $('#client_id').val(),
        Description: $('#description').val(),
        RequestNotes: $('#notes').val(),
        Material: $('#material').val(),
        OtherMaterialNotes: $('.other-mat-entry').val(),
        SidesPrinted: $('input[name="oneTwoNone"]:checked').val(),
        FinishingInformation: $('#finishing_entry').val(),
        PackingInformation: $('#packing_entry').val(),
        RequestedByDate: $('#requested_by_date').val(),
        FileNotes: $('.file-notes').val()
    };

    model.items = new Array();

    $('.piece-list li').each(function () {
        $item = $(this);

        let i = {
            LengthInches: $item.find('.length-input').val(),
            WidthInches: $item.find('.width-input').val(),
            Quantity: $item.find('.quantity-input').val(),
            Versions: $item.find('.versions-input').val()
        };

        model.items.push(i);
    });

    model.files = new Array();

    let files = $('.files').prop('files'); // Get the FileList

    $.each(files, function (index, file) {
        let f = {
            name: file.name,
            size: file.size,
            type: file.type
        };
        model.files.push(f);
    });

    return model;
};

function AddPiece() {
    let $list = $('.piece-list');

    let html =
        `
        <li>
            <div class="field-binder-row-flex">
                <div class="field-binder-col-flex">
                    <label>LENGTH</label>
                    <input type="text" class="styled-input length-input ff-dm-sans fs-12" />
                </div>
                <div class="field-binder-col-flex">
                    <label>WIDTH</label>
                    <input type="text" class="styled-input width-input ff-dm-sans fs-12" />
                </div>
                <div class="field-binder-col-flex">
                    <label>QUANTITY</label>
                    <input type="number" class="styled-input quantity-input ff-dm-sans fs-12" />
                </div>
                <div class="field-binder-col-flex">
                    <label>VERSIONS</label>
                    <input type="number" class="styled-input versions-input ff-dm-sans fs-12" />
                </div>
            </div>
        </li>
        `;

    $list.append(html);
};

$(function () {
    $('#client_autocomplete').autocomplete({
        source: function (request, response) {
            $.ajax({
                url: ProcessUrl("AutocompleteClient"),
                type: 'GET',
                data: {
                    'q': request.term
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
                            defaultAddressId: item.DefaultAddress
                        }
                    }));
                }
            });
        },
        select: function (event, ui) {
            $('#client_id').val(ui.item.recordId);
        }
    });
})
    .on('change', 'select[name="MATERIAL"]', OtherMaterialEntryCheck)
    .on('click', '.add-piece-btn', AddPiece)
    .on('click', '.submit-request-btn', SubmitRequest);