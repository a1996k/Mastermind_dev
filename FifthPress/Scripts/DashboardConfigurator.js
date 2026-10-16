const removeCard = '<span class="remove-card">X</span>';
const addCard = '<span class="add-card">+</span>';
const removeUser = '<span class="remove-user">X</span>';
const addUser = '<span class="add-user">+</span>';
const configPermissions = '<i data-modal-to-open="#dashboard_permission_config_modal" class="fa-solid fa-gear px-3 config-perms"></i>';

function LoadActiveCards(activeCards) {
    $('ul.active-cards').html('');

    for (let i = 0; i < activeCards.length; i++) {
        let html =
            `
            <li class="active-card-entry" data-record-id="${activeCards[i].Item1}">${activeCards[i].Item2}${configPermissions}${removeCard}</li>
            `;

        $('ul.active-cards').append(html);
    }

    $('.active-card-col').show();
};

function LoadInactiveCards(inactiveCards) {
    $('ul.inactive-cards').html('');

    for (let i = 0; i < inactiveCards.length; i++) {
        let html =
            `
            <li class="inactive-card-entry" data-record-id="${inactiveCards[i].Item1}">${inactiveCards[i].Item2}${addCard}</li>
            `;

        $('ul.inactive-cards').append(html);
    }

    $('.inactive-card-col').show();
};

function LoadCardView(res) {
    LoadActiveCards(res.ActiveCards);
    LoadInactiveCards(res.InactiveCards);
};

function GetDashConfigUser() {
    return $('.dash-config-user-select').val();
};

function RemoveCard() {
    if (confirm('Note: this will clear all existing permissions for this chart. Continue?')) {
        let $pressed = $(this);
        let $card = $pressed.parents('.active-card-entry');

        MoveActiveToInactive($card);
    }
};

function AddCard() {
    let $pressed = $(this);
    let $card = $pressed.parents('.inactive-card-entry');

    MoveInactiveToActive($card);
};

function MoveInactiveToActive($inactiveCard) {
    let $newRow = $inactiveCard.clone();
    $newRow.removeClass('inactive-card-entry');
    $newRow.addClass('active-card-entry');
    $newRow.find('.add-card').remove();
    $newRow.append($(configPermissions));
    $newRow.append($(removeCard));
    $('ul.active-cards').append($newRow);
    $inactiveCard.remove();

    //ajax request to add access to this card

    let userId = $('.dash-config-user-select').val();
    let cardId = $newRow.attr('data-record-id');

    $.post(ProcessUrl('add-card-permission') + '/' + userId + '/' + cardId);
};
function MoveActiveToInactive($activeCard) {
    let $newRow = $activeCard.clone();
    $newRow.removeClass('active-card-entry');
    $newRow.addClass('inactive-card-entry');
    $newRow.find('.remove-card').remove();
    $newRow.find('.config-perms').remove();
    $newRow.append($(addCard));
    $('ul.inactive-cards').append($newRow);
    $activeCard.remove();

    let userId = $('.dash-config-user-select').val();
    let cardId = $newRow.attr('data-record-id');

    //ajax request to remove permissions/access to this card
    $.post(ProcessUrl('remove-card-permission') + '/' + userId + '/' + cardId);
};

function AddUserToPermissions() {
    let $pressed = $(this);
    let $userEntry = $pressed.parents('.inactive-user-entry');
    let requestedUserId = $userEntry.attr('data-record-id');
    let requestingUserId = $('.dash-config-user-select').val();
    let cardId = $('.active-card-id').val();

    //move user from unassigned to assigned list
    let $newRow = $userEntry.clone();
    $newRow.removeClass('inactive-user-entry');
    $newRow.addClass('active-user-entry');
    $newRow.find('.add-user').remove();
    $newRow.append($(removeUser));
    $('#dashboard_permission_config_modal ul.assigned-users').append($newRow);
    $userEntry.remove();

    //ajax request to add user permission
    $.post(ProcessUrl('add-user-to-card') + '/' + requestingUserId + '/' + requestedUserId + '/' + cardId);
};

function RemoveUserFromPermissions() {
    let $pressed = $(this);
    let $userEntry = $pressed.parents('.active-user-entry');
    let requestedUserId = $userEntry.attr('data-record-id');
    let requestingUserId = $('.dash-config-user-select').val();
    let cardId = $('.active-card-id').val();

    //move user from assigned to unassigned list
    let $newRow = $userEntry.clone();
    $newRow.removeClass('active-user-entry');
    $newRow.addClass('inactive-user-entry');
    $newRow.find('.remove-user').remove();
    $newRow.append($(addUser));
    $('#dashboard_permission_config_modal ul.unassigned-users').append($newRow);
    $userEntry.remove();

    //ajax request to remove user permission
    $.post(ProcessUrl('remove-user-from-card') + '/' + requestingUserId + '/' + requestedUserId + '/' + cardId);
};

function GetUserDashboardCards() {
    let userId = $('.dash-config-user-select').val();

    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-dash-config') + '/' + userId,
        success: function (data) {
            try {
                let res = JSON.parse(data);
                LoadCardView(res);
            } catch (e) { }
        }
    })
};

function LoadUserPermissions(recs) {

    let $assignedUsersList = $('#dashboard_permission_config_modal ul.assigned-users');
    let $unassignedUsersList = $('#dashboard_permission_config_modal ul.unassigned-users');
    //set the modal header text
    $('#dashboard_permission_config_modal .db-card-title').html(recs.CardTitle);
    $('#dashboard_permission_config_modal .db-requestor-name').html(recs.RequestingUserFullName);

    $unassignedUsersList.html('');

    //UnassignedUsers
    for (let i = 0; i < recs.UnassignedUsers.length; i++) {
        let newHtml =
            `<li class="inactive-user-entry" data-record-id="${recs.UnassignedUsers[i].UserId}">${recs.UnassignedUsers[i].FirstName} ${recs.UnassignedUsers[i].LastName}${addUser}</li>`;

        $unassignedUsersList.append(newHtml);
    }

    $assignedUsersList.html('');

    //ExistingPermissions
    for (let i = 0; i < recs.AssignedUsers.length; i++) {
        let newHtml =
            `<li class="active-user-entry" data-record-id="${recs.AssignedUsers[i].UserId}">${recs.AssignedUsers[i].FirstName} ${recs.AssignedUsers[i].LastName}${removeUser}</li>`;

        $assignedUsersList.append(newHtml);
    }
};

function LoadCardUserPermissions() {
    let $pressed = $(this);
    let cardId = $pressed.parents('li').attr('data-record-id');
    //get the user id and the card id
    //ajax request for individual permissions (currently list of users that this user has access to in this chart)
    let userId = $('.dash-config-user-select').val();

    //store the cardId for use when adding/removing users
    $('.active-card-id').val(cardId);

    $.ajax({
        type: 'GET',
        url: ProcessUrl('user-chart-perms') + '/' + userId + '/' + cardId,
        success: function (data) {
            try {
                let res = JSON.parse(data);
                LoadUserPermissions(res);

                //load ^ data into the permissions modal
            } catch (e) {
                console.log(e);
            }
        }
    })
};
$(function () {
})
    .on('change', '.dash-config-user-select', GetUserDashboardCards)
    .on('click', 'ul .remove-card', RemoveCard)
    .on('click', 'ul .add-card', AddCard)
    .on('click', '.config-perms', LoadCardUserPermissions)
    .on('click', '.add-user', AddUserToPermissions)
    .on('click', '.remove-user', RemoveUserFromPermissions);