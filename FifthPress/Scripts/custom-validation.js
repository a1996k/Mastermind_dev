$(function () { })
    .on('blur', '._val_-gt-zero', function () {
        var $this = $(this);
        var val = $this.val();
        if (val <= 0) {
            $this.val('0');
        }
    });