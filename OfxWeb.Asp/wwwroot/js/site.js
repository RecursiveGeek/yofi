﻿$(document).ready(function () {

    // jQuery creates it's own event object, and it doesn't have a
    // dataTransfer property yet. This adds dataTransfer to the event object.
    // Thanks to l4rk for figuring this out!
    jQuery.event.props.push('dataTransfer');

    $(".apply-link").on("click", function (event) {
        event.preventDefault();
        applyPayee($(this).parents('tr'));
    });

    $(".checkbox-hidden").on("click", function (event) {
        var endpoint = $(this).is(":checked") ? "Hide" : "Show";
        $.ajax({
            url: "/api/tx/" + endpoint + "/" + this.dataset.id
        });
    });

    $('.actiondialog').on('show.bs.modal', function (event) {
        var button = $(event.relatedTarget) // Button that triggered the modal
        var modal = $(this);
        var form = modal.find('form');
        var tr = button.parents('tr');
        form.data('tr', tr);
        var id = tr.data('id')
        var endpoint = modal.data('endpoint')

        // Fill in the "More..." button
        var needsid = modal.find('.asp-route-id');
        var originalhref = needsid.attr('originalhref');
        if (originalhref == null) {
            originalhref = needsid.attr('href');
            needsid.attr('originalhref',originalhref);
        }

        var newhref = originalhref + '/' + id;
        needsid.attr('href', newhref);

        $.ajax({
            url: endpoint + id,
            success: function (htmlresult) {
                modal.find('.modal-body').html(htmlresult);
            },
            error: function (result) {
                alert(result.responseText);
                modal.find('.modal-body').text(result.responseText);
            }            
        });
    })

    $('#editModal form').submit( function (event)
    {
        event.preventDefault();
        var tr = $(this).data('tr');

        $.ajax({
            url: "/api/tx/Edit/5",
            type: "POST",
            data: $(this).serialize(),
            success: function (jsonresult) {
                var result = JSON.parse(jsonresult);

                if (result.Ok) {
                    tr.find('.display-payee').text(result.Transaction.Payee);
                    tr.find('.display-memo').text(result.Transaction.Memo);
                    tr.find(".display-category").text(result.Transaction.Category);
                    tr.find(".display-subcategory").text(result.Transaction.SubCategory);
                }
                else
                    alert(result.Exception.Message);
            }
        });
        $(this).parents('.modal').modal('hide');
    });

    $('#editPayeeModal form').submit( function (event)
    {
        event.preventDefault();
        var tr = $(this).data('tr');

        $.ajax({
            url: "/api/tx/EditPayee/5",
            type: "POST",
            data: $(this).serialize(),
            success: function (jsonresult) {
                var result = JSON.parse(jsonresult);

                if (result.Ok) {
                    tr.find('.display-payee').text(result.Payee.Name);
                    tr.find(".display-category").text(result.Payee.Category);
                    tr.find(".display-subcategory").text(result.Payee.SubCategory);
                }
                else
                    alert(result.Exception.Message);
            }
        });
        $(this).parents('.modal').modal('hide');
    });

    $('#addPayeeModal form').submit(function (event)
    {
        event.preventDefault();
        var tr = $(this).data('tr');

        $.ajax({
            url: "/api/tx/AddPayee/",
            type: "POST",
            data: $(this).serialize(),
            success: function (jsonresult) {
                var result = JSON.parse(jsonresult);
                if (result.Ok)
                    applyPayee(tr);
                else
                    alert(result.Exception.Message);
            }
        });
        $(this).parents('.modal').modal('hide');
    });

    $('.txdrop').on('drop', function (event) {

        event.preventDefault();
        if (event.dataTransfer.items) {
            // Use DataTransferItemList interface to access the file(s)
            for (var i = 0; i < event.dataTransfer.items.length; i++) {
                // If dropped items aren't files, reject them
                if (event.dataTransfer.items[i].kind === 'file') {
                    var file = event.dataTransfer.items[i].getAsFile();

                    var tr = $(this);
                    var id = tr.data('id');

                    let formData = new FormData()
                    formData.append('file', file)
                    formData.append('id', id)

                    $.ajax({
                        url: "/api/tx/UpReceipt/5",
                        type: "POST",
                        data: formData,
                        processData: false,
                        contentType: false,
                        error: function (result) {
                            alert(result.responseText);
                        },
                        success: function (jsonresult) {
                            var result = JSON.parse(jsonresult);

                            if (result.Ok) {
                                tr.find('.display-receipt').children().show();
                                alert('Ok');
                            }
                            else
                                alert(result.Exception.Message);
                        }
                    });

                }
            }
        } else {
            // Use DataTransfer interface to access the file(s)
            for (var i = 0; i < ev.dataTransfer.files.length; i++) {
                alert('... file[' + i + '].name = ' + ev.dataTransfer.files[i].name);
            }
        }
    });
});

function applyPayee(tr)
{
    var id = tr.data('id');

    $.ajax({
        url: "/api/tx/ApplyPayee/" + id,
        success: function (jsonresult) {
            var result = JSON.parse(jsonresult);

            if (result.Ok) {
                tr.find(".display-category").text(result.Payee.Category);
                tr.find(".display-subcategory").text(result.Payee.SubCategory);
            }
            else
                alert(result.Exception.Message);
        }
    });
}
