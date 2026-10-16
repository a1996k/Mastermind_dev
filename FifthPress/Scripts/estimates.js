/*select table vars for datatable*/
var qst, pst, sst;

var materialModel = new Array();
var serviceModel = new Array();

class ServiceModel{
    constructor(itemId){
        this.id = itemId;
    }

    add(serviceModelEntry){
        serviceModel.push(serviceModelEntry);
    }
}

class ProductModel{
    constructor(itemId){
        this.id = itemId;
    }
}

class ServiceModelEntry{
    //add any other necessary parameters to this constructor
    constructor(itemId, serviceId){
        this.id = itemId;
        this.service = serviceId;
    }
}

class ProductModelEntry{
    constructor(itemId, productId){
        this.id = itemId;
        this.product = productId;
    }
}

function StepTransition(){
    $('.step:not(.d-none)').addClass('d-none');
    $('.step[data-step-num="' + $(this).attr('data-go-to') + '"]').removeClass('d-none');
};

function GetEstimates(){
    if(typeof qst !== 'undefined'){
        qst.destroy(false);
    }
    $.ajax({
        type: 'GET',
        url: ProcessUrl('Estimates/Get/All'),
        success: function(data){
            var dataJ = JSON.parse(data);

            $('#estimate_select_table thead').html(dataJ.TableHead);
            $('#estimate_select_table tbody').html(dataJ.TableBody);

            qst = $('#estimate_select_table').DataTable({
                "paging": false
            });
        },
        error: function(err){
            console.error(err);
        }
    });
};

function AddButtonHandler(){
    var checkedItems = $('#materials_select input[type="checkbox"]:checked').length;

    if(checkedItems > 0){
        $('.ps-add-btn').prop('disabled', false);
    }else{
        $('.ps-add-btn').prop('disabled', true);
    }
};

function AddRowToTable(){
    var masterEstimateId = $('input[name="Estimate.Id"').val();

    $.get((ProcessUrl('add-item/estimate/') + masterEstimateId), function(data){
        $('#job_items').append(JSON.parse(data));
        NonTableRowRemoveAddHandler('job_items');
    });
    
    //need to make hidden divs as well
};

function RemoveRowFromTable(){
    var $row = $(this).parent();
    var rowId = $row.attr('data-record-id');
    
    $.post((ProcessUrl('remove-item/estimate/') + rowId), function(){
        $row.remove();
        NonTableRowRemoveAddHandler('job_items');
    });
};

//4-20-2023 update to select Services and not include items (products/materials)
function CreateServiceSelect(prodId){
    //creates an id like service_select_{prodId}_{# instance of this product}
    var newId = ('service_select_' + prodId + '_' + ($('div[id^="service_select_' + prodId + '"]').length + 1)).toString();

    //create a new div with id newId inside of #service_select_forms
    $('#service_select_form').append('<div id="' + newId + '" class="service-select-form"></div>');

    $.ajax({
        type: 'GET',
        url: ProcessUrl('Services/Get/All'),
        success: function(data){
            $('#' + newId).html(data);
        },
        error: function(error){
            console.log(error);
        }
    })
};


//these should be events on modal shown/hide
function SelectServices(){
    var sid = $(this).parent().parent().attr('data-record-id');
    $('#sp_adder_helper').val(sid);
};

function SelectMaterials(){
    var pid = $(this).parent().parent().attr('data-record-id');
    $('#sp_adder_helper').val(pid);
};

function SwapViews(){
    var $view1 = $('.catted-wrapper');
    var $view2 = $('.uncatted-wrapper');

    if($view1.hasClass('slide-i-left')){
        $view1.removeClass('slide-i-left').addClass('slide-o-left');
        $view2.removeClass('slide-o-right').addClass('slide-i-right');
    }else{
        $view1.removeClass('slide-o-left').addClass('slide-i-left');
        $view2.removeClass('slide-i-right').addClass('slide-o-right');
    }
};

function CustomFormSerialization(evt){
    evt.preventDefault();
    //declare a new array for the objects (serializing the items)
    var arr = [];
    $('.estimate-item-row').each(function(){
        var $row = $(this);
        var obj = {};
        obj['Id'] = $row.attr('data-record-id');
        //set the Title property of obj to the value contained in EstimateItems.Title
        obj['Title'] = $row.find('input[name="EstimateItems.Title"]').val();
        obj['Quantity'] = $row.find('input[name="EstimateItems.Quantity').val();
        //repeat this for Sheets, Sheets-Unit, PriceLevelId, RoundPrice, FormatUnitPrice, Width and Height
        obj['Sheets'] = $row.find('input[name="EstimateItems.Sheets"]').val();
        obj['SheetsUnit'] = $row.find('select[name="EstimateItems.SheetsUnit"]').val();
        obj['PriceLevelId'] = $row.find('select[name="EstimateItems.PriceLevelId"]').val();
        obj['RoundPrice'] = $row.find('input[name="EstimateItems.RoundPrice"]').is(':checked');
        obj['FormatUnitPrice'] = $row.find('input[name="EstimateItems.FormatUnitPrice"]').val();
        obj['Width'] = $row.find('input[name="EstimateItems.Width"]').val();
        obj['Height'] = $row.find('input[name="EstimateItems.Height"]').val();
        obj['Estimate'] = $('input[name="Estimate.Id"]').val();

        arr.push(obj);
    });

    var form = $('#estimate_builder').serialize();
    //form = form.replace('ClientSL', 'Estimate.Client');
    form = JSON.parse('{"' + decodeURI(form.replace(/&/g, "\",\"").replace(/=/g, "\":\"").replace(/Estimate./g,"") + '"}'));

    $.ajax({
        type: 'POST',
        url: ProcessUrl('Estimate/Update'),
        data: {
            q: form,
            items: arr
        },
        success: function(data){
            console.log(data);
        },
        error: function(data){
            console.error('Error in Estimate/Update POST ajax call');
        }
    });
};

function AddServicesToItem(){
    var list = $('#service_select_table');
    var itemId = $('#sp_adder_helper').val();

    //get the data-product-id and data-item-title attributes from all checked boxes where data-record-type is service as an array
    var arr = list.find('input[type="checkbox"]:checked[data-record-type="service"]').map(function(){
        return {
            ProductId: $(this).attr('data-product-id'),
            ItemTitle: $(this).attr('data-item-title')
        };
    }).get();
    
    //create li items from arr
    var lis = arr.map(function(item){
        return '<li data-product-id="' + item.ProductId + '" data-item-title="' + item.ItemTitle + '">' + item.ItemTitle + '</li>';
    });
    
    //add lis items to the .product-service-list
    $('.item-row[data-record-id="' + itemId + '"] .product-service-list').append(lis);
    
    console.log(arr);
};

function AddMaterialsToItem(){
    var list = $('#materials_select_table');
    var itemId = $('#sp_adder_helper').val();

    //get the data-product-id attribute from all checked boxes where data-record-type is product as an array
    var arr = list.find('input[type="checkbox"]:checked[data-record-type="product"]').map(function(){
        return {
            ProductId: $(this).attr('data-product-id'),
            ItemTitle: $(this).attr('data-item-title')
        };
    }).get();

    //create li items from arr
    var lis = arr.map(function(item){
        return '<li data-product-id="' + item.ProductId + '" data-item-title="' + item.ItemTitle + '">' + item.ItemTitle + '</li>';
    });

    //add lis items to the .product-service-list
    $('.item-row[data-record-id="' + itemId + '"] .product-service-list').append(lis);

    console.log(arr);

};

function ClearProductChecks(){
    $('#materials_select_table input[type="checkbox"]:checked').prop('checked', false);
};

function ClearServiceChecks(){
    $('#service_select_table input[type="checkbox"]:checked').prop('checked', false);
};

function AjaxNewClient(){

};

function AjaxEditClient(){

};

$(function(){
    sst = $('#service_select_table').DataTable({
        'columnDefs': [{
                orderable: false,
                targets: 3
            }],
        'paging': false
    });
    pst = $('#materials_select_table').DataTable({
            'columnDefs': [{
                orderable: false,
                targets: 3
            }],
        'paging': false
    });
})
    .on('click', '.create-wrapper .quick-button,.gb-btn', StepTransition)
    .on('click', '#materials_select input[type="checkbox"]', AddButtonHandler)
    .on('click', '.item-services-btn', SelectServices)
    .on('click', '.item-materials-btn', SelectMaterials)
    .on('click', '.add-item-btn', AddRowToTable)
    .on('click', '#job_items .remove-btn', RemoveRowFromTable)
    .on('submit', '#estimate_builder', CustomFormSerialization)
    .on('click', '.modal .btn-group input[type="radio"]', SwapViews)
    .on('click', '.builder-service-add', AddServicesToItem)
    .on('click', '.builder-product-add', AddMaterialsToItem)
    .on('hide.bs.modal', '#materials_select_modal', ClearProductChecks)
    .on('hide.bs.modal', '#service_select_modal', ClearServiceChecks)
    .on('click', '.ajax-new-client', AjaxNewClient)
    .on('click', '.ajax-edit-client', AjaxEditClient);