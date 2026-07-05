window.scrollToFooter = () => {
    const footer = document.getElementById("site-footer");
    if (footer) {
        footer.scrollIntoView({ behavior: "smooth" });
    }
};

// Renders the proof-of-purchase QR (booking id only) into the given element
// using the locally hosted qrcode library. Returns true when rendered.
window.renderBookingQr = (elementId, text) => {
    const target = document.getElementById(elementId);

    if (!target || typeof QRCode === "undefined") {
        return false;
    }

    target.innerHTML = "";

    new QRCode(target, {
        text: text,
        width: 220,
        height: 220,
        correctLevel: QRCode.CorrectLevel.M
    });

    return true;
};
