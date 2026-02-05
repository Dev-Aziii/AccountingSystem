window.renderRecaptcha = (containerId, siteKey) => {
    if (window.grecaptcha && window.grecaptcha.render) {
        // Clear previous instances if any to prevent "placeholder already created" error
        const container = document.getElementById(containerId);
        if (container) {
            container.innerHTML = '';
            try {
                grecaptcha.render(containerId, {
                    'sitekey': siteKey
                });
            } catch (error) {
                console.error("Recaptcha render error:", error);
            }
        }
    } else {
        console.warn("grecaptcha not loaded yet.");
    }
};

window.getCaptchaResponse = () => {
    if (window.grecaptcha) {
        return grecaptcha.getResponse();
    }
    return "";
};