window.recaptchaWidgetId = null;

window.renderRecaptcha = async (containerId, siteKey, maxAttempts = 10, retryDelayMs = 300) => {
    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
        if (window.grecaptcha && window.grecaptcha.render) {
            const container = document.getElementById(containerId);
            if (!container) {
                console.warn("reCAPTCHA container not found:", containerId);
                return false;
            }

            container.innerHTML = "";
            window.recaptchaWidgetId = null;

            try {
                window.recaptchaWidgetId = window.grecaptcha.render(containerId, {
                    sitekey: siteKey
                });
                return true;
            } catch (error) {
                console.error("reCAPTCHA render error:", error);
                return false;
            }
        }

        await new Promise(resolve => setTimeout(resolve, retryDelayMs));
    }

    console.warn("grecaptcha did not load after retrying.");
    return false;
};

window.getCaptchaResponse = () => {
    if (!window.grecaptcha) {
        return "";
    }

    if (window.recaptchaWidgetId !== null && window.recaptchaWidgetId !== undefined) {
        return window.grecaptcha.getResponse(window.recaptchaWidgetId);
    }

    return window.grecaptcha.getResponse();
};

window.resetCaptcha = () => {
    if (!window.grecaptcha) {
        return;
    }

    try {
        if (window.recaptchaWidgetId !== null && window.recaptchaWidgetId !== undefined) {
            window.grecaptcha.reset(window.recaptchaWidgetId);
            return;
        }

        window.grecaptcha.reset();
    } catch (error) {
        console.warn("reCAPTCHA reset error:", error);
    }
};
