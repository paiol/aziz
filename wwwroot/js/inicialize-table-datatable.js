$(document).ready(function () {
    $('.data-table').DataTable({
        language: {
            search: "",
            searchPlaceholder: "Procurar...",
            lengthMenu: "_MENU_",
            info: "_START_-_END_ de _TOTAL_",
            infoEmpty: "0 registos",
            infoFiltered: "(filtrado de _MAX_ registos)",
            zeroRecords: "Nenhum registo encontrado",
            paginate: { previous: "Anterior", next: "Seguinte" }
        },
        aLengthMenu: [[10, 25, 50, 100, -1], [10, 25, 50, 100, "Todos"]],
        processing: true,
        order: [],
        deferRender: true,
        autoWidth: false,
        pagingType: 'simple_numbers'
    });
});
