﻿// Write your JavaScript code.
$(document).ready(function () {
    $(".apply-link").on("click", function (event) {
        event.preventDefault();
        var id = this.dataset.id;
        var target_cat = $(this).parent().siblings(".display-category");
        var target_subcat = $(this).parent().siblings(".display-subcategory");
        var url = "/api/tx/ApplyPayee/" + id;
        $.ajax({
            url: url,
            success: function (result) {
                var payee = JSON.parse(result);

                if ("Category" in payee)
                    target_cat.html(payee.Category);
                if ("SubCategory" in payee)
                    target_subcat.html(payee.SubCategory);
            }
        });
    });
    $(".checkbox-hidden").on("click", function (event) {
        var id = this.dataset.id;

        var endpoint = "Show";
        if ($(this).is(":checked"))
            endpoint = "Hide";

        var url = "/api/tx/" + endpoint + "/" + id;
        $.ajax({
            url: url,
        });
    });

    $('#editModal').on('show.bs.modal', function (event) {
        var button = $(event.relatedTarget) // Button that triggered the modal
        $(this).data('trigger',button);
        var id = button.data('id') // Extract info from data-* attributes
        // If necessary, you could initiate an AJAX request here (and then do the updating in a callback).
        // Update the modal's content. We'll use jQuery here, but you could use a data binding library or other methods instead.
        var modal = $(this);

        var url = "/Transactions/EditModal/" + id;
        /*
        $.get(url, function (data)
        {
            modal.find('.modal-body').html(data);
        });
        */
        $.ajax({
            url: url,
            success: function (result) {
                modal.find('.modal-body').html(result);
            },
            error: function (result) {
                alert(result.responseText);
                modal.find('.modal-body').text(result.responseText);
            }
            
        });
    })

    $("#editModal .btn-primary").on("click", function (event) {

        var data = $('#EditPartialForm').serialize();
        var url = "/api/tx/Edit/5";        

        $.post(url, data, function (resultjson)
        {
            var result = JSON.parse(resultjson);
            if (result.Item1 == "OK") {
                var trigger = $('#editModal').data('trigger');
                var td = trigger.parent();
                var payee = td.siblings('.display-payee');
                var memo = td.siblings('.display-memo');
                var category = td.siblings(".display-category");
                var subcategory = td.siblings(".display-subcategory");
                payee.text(result.Item2.Payee);
                memo.text(result.Item2.Memo);
                category.text(result.Item2.Category);
                subcategory.text(result.Item2.SubCategory);

            }
            else
                alert(result);            
        });

    });

});
