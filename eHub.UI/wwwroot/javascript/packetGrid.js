window.packetGrid = {
    attachGridEvents: function (dotNetObj, gridRef) {

        var gridElements = gridRef.getElementsByClassName("mud-table-container")

        if (gridElements.length == 0 || gridElements[0] == undefined) {
            return;
        }

        var gridContainer = gridElements[0];

        let timeout;

        gridContainer.addEventListener("scroll", (event) => {

            if (document.getElementsByClassName("HandleScroll").length == 0) {
                gridContainer.classList.add("HandleScroll");
            }

            this.handleGridScroll(gridContainer, dotNetObj);

            clearTimeout(timeout);

            timeout = setTimeout(function () {
                gridContainer.classList.remove("HandleScroll");
            }, 100);
        });
    },

    handleGridScroll: function (gridContainer, dotNetObj) {

        if (!gridContainer.classList.contains("HandleScroll"))
            return;

        //console.log("scroll", dotNetObj)
        var rows = gridContainer.getElementsByClassName("mud-table-row");
        var scrollTop = gridContainer.scrollTop;

        for (var i = 1; i < rows.length; i++) {
            var rowTop = rows[i].offsetTop;

            if (scrollTop <= rowTop) {
                var rowClass = rows[i].getAttribute("class");
                dotNetObj.invokeMethodAsync("HandleScroll", rowClass);
                break;
            }
        }
    },

    scrollToItemWithClass: function (gridRef, itemClassName) {
        var gridElements = gridRef.getElementsByClassName("mud-table-container")

        if (gridElements.length == 0 || gridElements[0] == undefined) {
            return;
        }

        var container = gridElements[0];

        var items = container.getElementsByClassName(itemClassName);

        if (items == undefined || items.length == 0)
            return;

        var itemTop = items[0].offsetTop;
        container.scrollTop = itemTop;
    },
};
