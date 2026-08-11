$(document).ready(function () {
    $(document).on('click', '.js-delete-btn', function (e) {
        e.preventDefault();

        var $btn = $(this);
        var url = $btn.data('url');
        var id = $btn.data('id');
        var token = $('#globalAntiForgeryForm input[name="__RequestVerificationToken"]').val();

        Swal.fire({
            title: 'Tem a certeza?',
            text: 'Esta ação não pode ser revertida.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Apagar',
            cancelButtonText: 'Cancelar',
            confirmButtonColor: '#d9534f'
        }).then(function (result) {
            if (!result.isConfirmed) return;

            var $form = $('<form>', { method: 'POST', action: url });
            $form.append($('<input>', { type: 'hidden', name: '__RequestVerificationToken' }).val(token));
            if (id) $form.append($('<input>', { type: 'hidden', name: 'id' }).val(id));
            $('body').append($form);
            $form.trigger('submit');
        });
    });
});
