(function () {
    "use strict";

    const fractionValues = {
        "¼": 0.25,
        "½": 0.5,
        "¾": 0.75,
        "⅓": 1 / 3,
        "⅔": 2 / 3,
        "⅛": 0.125,
        "⅜": 0.375,
        "⅝": 0.625,
        "⅞": 0.875
    };
    const quantityPattern = /^(\s*)(\d+\s+\d+\/\d+|\d+[¼½¾⅓⅔⅛⅜⅝⅞]|\d+\/\d+|\d+(?:[.,]\d+)?|[¼½¾⅓⅔⅛⅜⅝⅞])(\s*)(.+)$/;
    const numberFormatter = new Intl.NumberFormat("vi-VN", {
        maximumFractionDigits: 3
    });

    function parseQuantity(value) {
        if (fractionValues[value] !== undefined) return fractionValues[value];

        const mixedFraction = value.match(/^(\d+)\s+(\d+)\/(\d+)$/);
        if (mixedFraction) {
            const denominator = Number(mixedFraction[3]);
            return denominator > 0
                ? Number(mixedFraction[1]) + Number(mixedFraction[2]) / denominator
                : null;
        }

        const wholeWithFraction = value.match(/^(\d+)([¼½¾⅓⅔⅛⅜⅝⅞])$/);
        if (wholeWithFraction) {
            return Number(wholeWithFraction[1]) + fractionValues[wholeWithFraction[2]];
        }

        const fraction = value.match(/^(\d+)\/(\d+)$/);
        if (fraction) {
            const denominator = Number(fraction[2]);
            return denominator > 0 ? Number(fraction[1]) / denominator : null;
        }

        const decimal = Number(value.replace(",", "."));
        return Number.isFinite(decimal) ? decimal : null;
    }

    function scaleLine(line, multiplier) {
        const match = line.match(quantityPattern);
        if (!match) return line;
        if (!match[3] && !/^\p{L}/u.test(match[4])) return line;

        const quantity = parseQuantity(match[2]);
        if (quantity === null || quantity <= 0) return line;

        const scaledQuantity = quantity * multiplier;
        if (!Number.isFinite(scaledQuantity) || scaledQuantity <= 0) return line;

        return match[1]
            + numberFormatter.format(Number(scaledQuantity.toFixed(3)))
            + match[3]
            + match[4];
    }

    document.addEventListener("DOMContentLoaded", function () {
        const control = document.querySelector("[data-serving-scaler]");
        if (!control) return;

        const baseServings = Number(control.dataset.baseServings);
        const input = control.querySelector("#recipeServings");
        const status = control.querySelector("#servingSizeStatus");
        const ingredientLines = document.querySelectorAll("[data-scalable-ingredient]");
        const stepperButtons = control.querySelectorAll("[data-serving-change]");

        if (!Number.isInteger(baseServings) || baseServings < 1 || !input || !status) return;

        const minimum = Number(input.min);
        const maximum = Number(input.max);
        const baseLines = Array.from(ingredientLines, ingredient =>
            ingredient.dataset.original ?? ingredient.textContent ?? "");
        let currentServings = baseServings;

        function setStatus(message, invalid) {
            status.textContent = message;
            status.classList.toggle("is-error", invalid);
        }

        function updateStepperButtons() {
            stepperButtons.forEach(button => {
                const change = Number(button.dataset.servingChange);
                button.disabled = change < 0
                    ? currentServings <= minimum
                    : currentServings >= maximum;
            });
        }

        function updateIngredients(servings) {
            const multiplier = servings / baseServings;
            ingredientLines.forEach((ingredient, index) => {
                ingredient.textContent = servings === baseServings
                    ? baseLines[index]
                    : scaleLine(baseLines[index], multiplier);
            });
        }

        function applyServings(value) {
            const servings = Number(value);
            if (!Number.isInteger(servings) || servings < minimum || servings > maximum) {
                setStatus(`Nhập số khẩu phần từ ${minimum} đến ${maximum}.`, true);
                return false;
            }

            currentServings = servings;
            input.value = String(servings);
            updateIngredients(servings);
            updateStepperButtons();
            setStatus(`Nguyên liệu đang tính cho ${servings} khẩu phần.`, false);
            return true;
        }

        input.addEventListener("input", function () {
            applyServings(input.value);
        });

        input.addEventListener("blur", function () {
            if (!applyServings(input.value)) applyServings(currentServings);
        });

        stepperButtons.forEach(button => {
            button.addEventListener("click", function () {
                applyServings(currentServings + Number(button.dataset.servingChange));
            });
        });

        applyServings(baseServings);
    });
})();
