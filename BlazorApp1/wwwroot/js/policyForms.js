window.policyForms = {
    getScrollState: function (element) {
        if (!element) return { isAtBottom: false };

        const threshold = 12;
        const isAtBottom =
            element.scrollTop + element.clientHeight >= element.scrollHeight - threshold;

        return { isAtBottom };
    }
};