System.register(['vue', '@Obsidian/Utility/guid', '@Obsidian/Core/Controls/financialGateway', '@Obsidian/Enums/Controls/gatewayEmitStrings'], function (exports) {
  'use strict';
  var defineComponent, onMounted, openBlock, createElementBlock, newGuid, onSubmitPayment, GatewayEmitStrings;
  return {
    setters: [function (module) {
      defineComponent = module.defineComponent;
      onMounted = module.onMounted;
      openBlock = module.openBlock;
      createElementBlock = module.createElementBlock;
    }, function (module) {
      newGuid = module.newGuid;
    }, function (module) {
      onSubmitPayment = module.onSubmitPayment;
    }, function (module) {
      GatewayEmitStrings = module.GatewayEmitStrings;
    }],
    execute: function () {
      var styleId = 'epay-gateway-control-style';
      var fontsId = 'epay-gateway-control-fonts';

      function ensureStyleTag() {
        if (!document.getElementById(fontsId)) {
          var fontLink = document.createElement('link');
          fontLink.id = fontsId;
          fontLink.rel = 'stylesheet';
          fontLink.href = 'https://fonts.googleapis.com/css2?family=Manrope:wght@500;600;700;800&family=Plus+Jakarta+Sans:wght@400;500;600;700&display=swap';
          document.head.appendChild(fontLink);
        }

        if (document.getElementById(styleId)) {
          return;
        }

        var style = document.createElement('style');
        style.id = styleId;
        style.type = 'text/css';
        style.textContent = [
          ".epayWrap { --epay-bg:#fff; --epay-border:#d7d7d7; --epay-text:#1f2933; --epay-muted:#6b7280; --epay-danger:#c22016; --epay-radius-xl:22px; --epay-radius-lg:14px; --epay-radius-md:10px; --epay-radius-pill:999px; background:var(--epay-bg); border:1px solid var(--epay-border); border-radius:var(--epay-radius-lg); color:var(--epay-text); font-family:'Plus Jakarta Sans','Segoe UI',sans-serif; padding:14px; position:relative; }",
          ".epayHeader { margin-bottom:10px; }",
          ".epayTitle { margin:0; font-family:Manrope,sans-serif; font-size:18px; font-weight:800; color:#222; text-transform:none; }",
          ".epaySubtitle { margin:4px 0 0; font-size:13px; color:#666; }",
          ".epayCardPreview { margin:12px 0; border-radius:12px; border:1px solid #cecece; background:#fff; padding:12px 14px; display:grid; gap:4px; transition:all .2s ease; }",
          ".epayCardPreview.brand-visa { border-color:#bcd2ff; background:linear-gradient(135deg,#eaf1ff,#f4f8ff); }",
          ".epayCardPreview.brand-mastercard { border-color:#ffd7c2; background:linear-gradient(135deg,#fff1e8,#fff8f3); }",
          ".epayCardPreview.brand-amex { border-color:#b7effa; background:linear-gradient(135deg,#e8fbff,#f3fdff); }",
          ".epayCardPreview.brand-discover { border-color:#fed7aa; background:linear-gradient(135deg,#fff4ec,#fff9f4); }",
          ".epayPreviewBrand { font-family:Manrope,sans-serif; font-size:11px; font-weight:800; letter-spacing:.05em; text-transform:uppercase; color:#505050; }",
          ".epayPreviewNumber { font-family:Manrope,sans-serif; font-size:18px; font-weight:800; letter-spacing:.04em; color:#1d1d1d; }",
          ".epayPreviewMeta { display:flex; justify-content:space-between; gap:8px; font-size:12px; color:#555; }",
          ".epayFields { display:grid; gap:10px; }",
          ".epayRow { display:grid; grid-template-columns:1fr; gap:10px; }",
          ".epayField { display:grid; gap:6px; }",
          ".epayField > span { font-family:Manrope,sans-serif; font-size:11px; font-weight:700; letter-spacing:.05em; text-transform:uppercase; color:#4a4a4a; }",
          ".epayFieldHead { display:flex; align-items:center; justify-content:space-between; gap:10px; }",
          ".epayFieldHead > span { margin:0; }",
          ".epayCardBrands { display:inline-flex; align-items:center; gap:5px; }",
          ".epayBrandIcon { display:inline-flex; width:34px; height:22px; border-radius:4px; overflow:hidden; transition:opacity .18s ease, filter .18s ease, transform .18s ease; box-shadow:0 1px 2px rgba(15,23,42,.08); }",
          ".epayBrandIcon svg { width:100%; height:100%; display:block; }",
          ".epayBrandIcon.isDimmed { opacity:.32; filter:grayscale(1); }",
          ".epayBrandIcon.isActive { transform:scale(1.08); box-shadow:0 2px 6px rgba(15,23,42,.18); }",
          ".epayInputWrap { border:1px solid #bfbfbf; border-radius:var(--epay-radius-md); background:#fff; display:flex; align-items:center; gap:8px; padding:0 10px; transition:border-color .2s ease, box-shadow .2s ease; }",
          ".epayInputWrap:focus-within { border-color:#7b7b7b; box-shadow:0 0 0 3px rgba(51,51,51,.12); }",
          ".epayInputWrap.isInvalid { border-color:#dc6e66; }",
          ".epayInput { width:100%; min-height:44px; border:0; background:transparent; padding:0; font-size:14px; color:#1e1e1e; box-shadow:none; outline:none; }",
          ".epayBrandTag { flex:0 0 auto; border-radius:var(--epay-radius-pill); border:1px solid #bfc6cf; background:#edf1f7; color:#405061; font-family:Manrope,sans-serif; font-size:10px; font-weight:800; letter-spacing:.06em; text-transform:uppercase; padding:4px 7px; }",
          ".epayBrandTag.brand-visa { background:#eaf1ff; border-color:#bcd2ff; color:#1e44a8; }",
          ".epayBrandTag.brand-mastercard { background:#fff1e8; border-color:#ffd7c2; color:#9a3412; }",
          ".epayBrandTag.brand-amex { background:#e8fbff; border-color:#b7effa; color:#0f6f88; }",
          ".epayBrandTag.brand-discover { background:#fff4ec; border-color:#fed7aa; color:#c2410c; }",
          ".epayHint { color:#727272; font-size:12px; }",
          ".epayError { color:var(--epay-danger); font-size:12px; font-weight:600; min-height:14px; }",
          ".epayInstallments { border-top:1px solid var(--epay-border); padding-top:12px; margin-top:4px; display:grid; gap:10px; }",
          ".epayCheckboxLabel { display:flex; align-items:center; gap:8px; font-family:Manrope,sans-serif; font-size:14px; font-weight:700; cursor:pointer; color:#1e1e1e; }",
          ".epayCheckboxLabel input[type='checkbox'] { width:18px; height:18px; accent-color:#000; cursor:pointer; }",
          ".epayInstallmentSelect { display:grid; gap:10px; }",
          ".epayInputWrap select.epayInput { appearance:auto; cursor:pointer; }",
          ".epaySurchargeInfo { display:flex; justify-content:space-between; align-items:center; background:#fff8e1; border:1px solid #ffe082; border-radius:var(--epay-radius-md); padding:10px 12px; }",
          ".epaySurchargeLabel { font-size:13px; color:#6d5c00; }",
          ".epaySurchargeAmount { font-family:Manrope,sans-serif; font-size:14px; font-weight:800; color:#b8860b; }",
          ".epayStateOverlay { position:fixed; inset:0; z-index:9999; background:rgba(0,0,0,.45); backdrop-filter:blur(4px); display:flex; align-items:center; justify-content:center; padding:20px; animation:epayFadeIn .3s ease; }",
          ".epayStateModal { background:#fff; border-radius:var(--epay-radius-xl); padding:34px 24px; width:min(100%,380px); text-align:center; box-shadow:0 24px 48px rgba(0,0,0,.25); animation:epayScaleUp .3s cubic-bezier(.175,.885,.32,1.275); }",
          ".epayStateTitle { margin:20px 0 8px; font-family:Manrope,sans-serif; font-size:22px; font-weight:800; color:#111; }",
          ".epayStateText { margin:0; color:#555; font-size:14px; line-height:1.5; }",
          ".epaySpinner { width:60px; height:60px; margin:0 auto; border:5px solid #eaeaea; border-top-color:#000; border-radius:50%; animation:epaySpin .9s linear infinite; }",
          "@keyframes epayFadeIn { from { opacity:0; } to { opacity:1; } }",
          "@keyframes epayScaleUp { from { opacity:0; transform:translateY(16px) scale(.95); } to { opacity:1; transform:translateY(0) scale(1); } }",
          "@keyframes epaySpin { to { transform:rotate(360deg); } }",
          "@media (min-width: 600px) { .epayRow { grid-template-columns:1fr 1fr; } }"
        ].join('\n');

        document.head.appendChild(style);
      }

      function detectCardBrand(pan) {
        if (!pan) return 'unknown';
        if (/^4/.test(pan)) return 'visa';
        if (/^(5[1-5]|2(?:2[2-9]|[3-6]\d|7[01]|720))/.test(pan)) return 'mastercard';
        if (/^3[47]/.test(pan)) return 'amex';
        if (/^(6011|65|64[4-9])/.test(pan)) return 'discover';
        return 'unknown';
      }

      function getBrandLabel(brand) {
        if (brand === 'visa') return 'Visa';
        if (brand === 'mastercard') return 'Mastercard';
        if (brand === 'amex') return 'American Express';
        if (brand === 'discover') return 'Discover';
        return 'Tarjeta';
      }

      function luhnCheck(pan) {
        if (!/^\d{12,19}$/.test(pan || '')) {
          return false;
        }

        var sum = 0;
        var shouldDouble = false;
        for (var i = pan.length - 1; i >= 0; i--) {
          var digit = Number(pan.charAt(i));

          if (shouldDouble) {
            digit *= 2;
            if (digit > 9) {
              digit -= 9;
            }
          }

          sum += digit;
          shouldDouble = !shouldDouble;
        }

        return sum % 10 === 0;
      }

      function formatCardNumberDigits(digits, brand) {
        var max = brand === 'amex' ? 15 : 19;
        var clean = (digits || '').slice(0, max);

        if (brand === 'amex') {
          var p1 = clean.slice(0, 4);
          var p2 = clean.slice(4, 10);
          var p3 = clean.slice(10, 15);
          return [p1, p2, p3].filter(Boolean).join(' ');
        }

        var parts = clean.match(/.{1,4}/g);
        return parts ? parts.join(' ') : clean;
      }

      function parseExpiry(value) {
        var digits = (value || '').replace(/[^\d]/g, '');
        if (digits.length !== 4) {
          return { month: 0, year: 0, valid: false };
        }

        var month = Number(digits.slice(0, 2));
        var yy = Number(digits.slice(2, 4));

        if (!Number.isFinite(month) || month < 1 || month > 12) {
          return { month: 0, year: 0, valid: false };
        }

        return { month: month, year: 2000 + yy, valid: true };
      }

      function isExpired(month, year) {
        var now = new Date();
        var currentMonth = now.getMonth() + 1;
        var currentYear = now.getFullYear();

        if (year < currentYear) return true;
        if (year === currentYear && month < currentMonth) return true;
        return false;
      }

      function sanitizeAmount(amount) {
        var value = Number(amount || 0);
        return Number.isFinite(value) && value > 0 ? value : 0;
      }

      function roundCurrency(value) {
        return Math.round((Number(value) || 0) * 100) / 100;
      }

      function formatAmount(value) {
        return roundCurrency(value).toFixed(2);
      }

      function visaSvg() {
        return "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 780 500' fill='none' aria-hidden='true'>" +
          "<g clip-path='url(#epay-visa-clip)'>" +
            "<path d='M780 0H0V500H780V0Z' fill='#1434CB'/>" +
            "<path d='M489.823 143.111C442.988 143.111 401.134 167.393 401.134 212.256C401.134 263.706 475.364 267.259 475.364 293.106C475.364 303.989 462.895 313.731 441.6 313.731C411.377 313.731 388.789 300.119 388.789 300.119L379.123 345.391C379.123 345.391 405.145 356.889 439.692 356.889C490.898 356.889 531.19 331.415 531.19 285.784C531.19 231.419 456.652 227.971 456.652 203.981C456.652 195.455 466.887 186.114 488.122 186.114C512.081 186.114 531.628 196.014 531.628 196.014L541.087 152.289C541.087 152.289 519.818 143.111 489.823 143.111ZM61.3294 146.411L60.1953 153.011C60.1953 153.011 79.8988 156.618 97.645 163.814C120.495 172.064 122.122 176.868 125.971 191.786L167.905 353.486H224.118L310.719 146.411H254.635L198.989 287.202L176.282 167.861C174.199 154.203 163.651 146.411 150.74 146.411H61.3294ZM333.271 146.411L289.275 353.486H342.756L386.598 146.411H333.271ZM631.554 146.411C618.658 146.411 611.825 153.318 606.811 165.386L528.458 353.486H584.542L595.393 322.136H663.72L670.318 353.486H719.805L676.633 146.411H631.554ZM638.848 202.356L655.473 280.061H610.935L638.848 202.356Z' fill='white'/>" +
          "</g>" +
          "<defs><clipPath id='epay-visa-clip'><rect width='780' height='500' fill='white'/></clipPath></defs>" +
        "</svg>";
      }

      function mastercardSvg() {
        return "<svg xmlns='http://www.w3.org/2000/svg' viewBox='80 0 620 410' fill='none' aria-hidden='true'>" +
          "<path d='M780 0H0V500H780V0Z' fill='#253747'/>" +
          "<path d='M465.738 69.1387H313.812V342.088H465.738V69.1387Z' fill='#FF5A00'/>" +
          "<path d='M323.926 205.613C323.926 150.158 349.996 100.94 390 69.1387C360.559 45.9902 323.42 32 282.91 32C186.945 32 109.297 109.648 109.297 205.613C109.297 301.578 186.945 379.227 282.91 379.227C323.42 379.227 360.559 365.237 390 342.088C349.94 310.737 323.926 261.069 323.926 205.613Z' fill='#EB001B'/>" +
          "<path d='M670.711 205.613C670.711 301.578 593.062 379.227 497.098 379.227C456.588 379.227 419.449 365.237 390.008 342.088C430.518 310.231 456.082 261.069 456.082 205.613C456.082 150.158 430.012 100.94 390.008 69.1387C419.393 45.9902 456.532 32 497.041 32C593.062 32 670.711 110.154 670.711 205.613Z' fill='#F79E1B'/>" +
        "</svg>";
      }

      function createMarkup(controlId, showNameField, installmentOpts) {
        // Sin overlay propio: el flujo de registro de Rock (host) ya muestra el estado de
        // "procesando" en el boton (isLoading + autoDisable) durante toda la operacion
        // (tokenizacion + cobro). Un overlay aqui solo cubre la tokenizacion (~1s) y se quita
        // antes de que el cobro termine, lo que se ve como un parpadeo que choca con el host.
        var html = "<div class='epayWrap'>" +
          "<div class='epayHeader'>" +
            "<h4 class='epayTitle'>Tarjeta</h4>" +
            "<p class='epaySubtitle'>Complete la informacion de pago segura.</p>" +
          "</div>" +
          "<div class='epayCardPreview brand-unknown' data-el='preview'>" +
            "<span class='epayPreviewBrand' data-el='previewBrand'>Tarjeta</span>" +
            "<strong class='epayPreviewNumber' data-el='previewNumber'>#### #### #### ####</strong>" +
            "<div class='epayPreviewMeta'><span data-el='previewName'>NOMBRE TITULAR</span><span data-el='previewExp'>MM/YY</span></div>" +
          "</div>" +
          "<div class='epayFields'>" +
            "<label class='epayField'>" +
              "<div class='epayFieldHead'>" +
                "<span>Numero de tarjeta</span>" +
                "<div class='epayCardBrands' role='img' aria-label='Tarjetas aceptadas'>" +
                  "<span class='epayBrandIcon' data-brand-icon='visa' title='Visa'>" + visaSvg() + "</span>" +
                  "<span class='epayBrandIcon' data-brand-icon='mastercard' title='Mastercard'>" + mastercardSvg() + "</span>" +
                "</div>" +
              "</div>" +
              "<div class='epayInputWrap' data-wrap='cardNumber'>" +
                "<input id='" + controlId + "-number' class='epayInput' type='text' maxlength='24' inputmode='numeric' autocomplete='cc-number' placeholder='4111 1111 1111 1111' />" +
                "<span class='epayBrandTag brand-unknown' data-el='brandTag'>Tarjeta</span>" +
              "</div>" +
              "<small class='epayError' data-error='cardNumber'></small>" +
            "</label>" +
            "<div class='epayRow'>" +
              "<label class='epayField'>" +
                "<span>Vencimiento</span>" +
                "<div class='epayInputWrap' data-wrap='expDate'><input id='" + controlId + "-exp' class='epayInput' type='text' maxlength='5' inputmode='numeric' autocomplete='cc-exp' placeholder='MM/YY' /></div>" +
                "<small class='epayHint'>Formato: MM/YY</small>" +
                "<small class='epayError' data-error='expDate'></small>" +
              "</label>" +
              "<label class='epayField'>" +
                "<span>CVV</span>" +
                "<div class='epayInputWrap' data-wrap='cvv'><input id='" + controlId + "-cvv' class='epayInput' type='password' maxlength='4' inputmode='numeric' autocomplete='cc-csc' placeholder='CVV (3)' /></div>" +
                "<small class='epayHint' data-el='cvvHint'>3 digitos para Tarjeta</small>" +
                "<small class='epayError' data-error='cvv'></small>" +
              "</label>" +
            "</div>" +
            "<label class='epayField' data-el='nameField' style='display:" + (showNameField ? "grid" : "none") + ";'>" +
              "<span>Nombre del titular</span>" +
              "<div class='epayInputWrap' data-wrap='cardName'><input id='" + controlId + "-name' class='epayInput' type='text' maxlength='120' autocomplete='cc-name' placeholder='Nombre como aparece en la tarjeta' /></div>" +
              "<small class='epayError' data-error='cardName'></small>" +
            "</label>";

        if (installmentOpts && installmentOpts.length > 0) {
          var selectOpts = '<option value="">-- Seleccionar --</option>';
          for (var k = 0; k < installmentOpts.length; k++) {
            var io = installmentOpts[k];
            selectOpts += '<option value="' + io.code + '">' + io.months + ' cuotas (+' + io.surcharge + '%)</option>';
          }
          html +=
            "<div class='epayInstallments' data-el='installmentSection'>" +
              "<label class='epayCheckboxLabel'><input type='checkbox' id='" + controlId + "-installcheck' /><span>Pago por cuotas</span></label>" +
              "<div data-el='installmentSelectWrap' style='display:none;'>" +
                "<label class='epayField'>" +
                  "<span>Seleccionar cuotas</span>" +
                  "<div class='epayInputWrap' data-wrap='installment'><select id='" + controlId + "-installselect' class='epayInput'>" + selectOpts + "</select></div>" +
                  "<small class='epayError' data-error='installment'></small>" +
                "</label>" +
                "<div class='epaySurchargeInfo' data-el='surchargeInfo' style='display:none;'>" +
                  "<span class='epaySurchargeLabel' data-el='surchargeLabel'></span>" +
                  "<strong class='epaySurchargeAmount' data-el='surchargeAmount'></strong>" +
                "</div>" +
              "</div>" +
            "</div>";
        }

        html += "</div></div>";
        return html;
      }

      var script = exports('default', defineComponent({
        __name: 'epayVisanetGatewayControl',
        props: {
          settings: {
            type: Object,
            required: true
          },
          amount: {
            type: Number,
            required: false,
            default: 0
          }
        },
        setup: function (__props, _ref) {
          var emit = _ref.emit;
          var controlId = 'epay-' + newGuid().replace(/-/g, '');
          var dom = null;

          var getPanDigits = function () {
            return dom && dom.cardNumber ? dom.cardNumber.value.replace(/[^\d]/g, '') : '';
          };

          var getBrand = function () {
            return detectCardBrand(getPanDigits());
          };

          var expectedCvvLength = function () {
            return getBrand() === 'amex' ? 4 : 3;
          };

          var setFieldError = function (key, message) {
            if (!dom) return;
            var error = dom.errors[key];
            var wrap = dom.wraps[key];
            if (error) {
              error.textContent = message || '';
            }
            if (wrap) {
              if (message) {
                wrap.classList.add('isInvalid');
              }
              else {
                wrap.classList.remove('isInvalid');
              }
            }
          };

          var showLoading = function (show) {
            if (dom && dom.loadingOverlay) {
              dom.loadingOverlay.style.display = show ? 'flex' : 'none';
            }
          };

          var installmentOpts = (__props.settings.enableInstallments && __props.settings.installmentOptions) ? __props.settings.installmentOptions : [];
          var useInstallments = false;
          var selectedInstallmentCode = '';

          var clearFieldErrors = function () {
            setFieldError('cardNumber', '');
            setFieldError('expDate', '');
            setFieldError('cvv', '');
            setFieldError('cardName', '');
            setFieldError('installment', '');
          };

          var updateBrandUI = function () {
            if (!dom) return;
            var brand = getBrand();
            var label = getBrandLabel(brand);
            dom.brandTag.className = 'epayBrandTag brand-' + brand;
            dom.brandTag.textContent = label;
            dom.previewBrand.textContent = label;
            dom.preview.className = 'epayCardPreview brand-' + brand;
            dom.cvv.placeholder = 'CVV (' + expectedCvvLength() + ')';
            dom.cvvHint.textContent = expectedCvvLength() + ' digitos para ' + label;

            if (dom.brandIcons && dom.brandIcons.length) {
              for (var i = 0; i < dom.brandIcons.length; i++) {
                var iconEl = dom.brandIcons[i];
                var iconBrand = iconEl.getAttribute('data-brand-icon');
                iconEl.classList.remove('isActive');
                iconEl.classList.remove('isDimmed');
                if (brand === 'unknown') {
                  // estado neutral: ningun icono atenuado
                }
                else if (iconBrand === brand) {
                  iconEl.classList.add('isActive');
                }
                else {
                  iconEl.classList.add('isDimmed');
                }
              }
            }
          };

          var updatePreview = function () {
            if (!dom) return;
            var brand = getBrand();
            var number = formatCardNumberDigits(getPanDigits(), brand);
            dom.previewNumber.textContent = number || '#### #### #### ####';
            var cardName = (dom.cardName && dom.cardName.value ? dom.cardName.value : '').trim();
            dom.previewName.textContent = cardName ? cardName.toUpperCase() : 'NOMBRE TITULAR';
            dom.previewExp.textContent = (dom.expDate.value || 'MM/YY');
          };

          var onCardNumberInput = function () {
            setFieldError('cardNumber', '');
            var digits = getPanDigits();
            dom.cardNumber.value = formatCardNumberDigits(digits, getBrand());
            updateBrandUI();
            updatePreview();
          };

          var onExpInput = function () {
            setFieldError('expDate', '');
            var digits = (dom.expDate.value || '').replace(/[^\d]/g, '').slice(0, 4);
            if (digits.length <= 2) {
              dom.expDate.value = digits;
            }
            else {
              dom.expDate.value = digits.slice(0, 2) + '/' + digits.slice(2);
            }
            updatePreview();
          };

          var onCvvInput = function () {
            setFieldError('cvv', '');
            dom.cvv.value = (dom.cvv.value || '').replace(/[^\d]/g, '').slice(0, 4);
          };

          var onNameInput = function () {
            setFieldError('cardName', '');
            updatePreview();
          };

          var validate = function () {
            clearFieldErrors();
            var errors = [];

            var pan = getPanDigits();
            if (!luhnCheck(pan)) {
              var msgPan = 'Numero de tarjeta invalido.';
              setFieldError('cardNumber', msgPan);
              errors.push({ name: 'Card Number', text: msgPan });
            }

            var expiry = parseExpiry(dom.expDate.value);
            if (!expiry.valid) {
              var msgExp = 'Usa el formato MM/YY.';
              setFieldError('expDate', msgExp);
              errors.push({ name: 'Expiration Date', text: msgExp });
            }
            else if (isExpired(expiry.month, expiry.year)) {
              var msgExp2 = 'La tarjeta esta vencida.';
              setFieldError('expDate', msgExp2);
              errors.push({ name: 'Expiration Date', text: msgExp2 });
            }

            var cvv = (dom.cvv.value || '').replace(/[^\d]/g, '');
            var validCvv = getBrand() === 'amex' ? cvv.length === 4 : cvv.length === 3;
            if (!validCvv) {
              var msgCvv = 'El CVV debe tener ' + expectedCvvLength() + ' digitos.';
              setFieldError('cvv', msgCvv);
              errors.push({ name: 'CVV', text: msgCvv });
            }

            if (__props.settings.promptForNameOnCard !== false) {
              var cardName = (dom.cardName.value || '').trim();
              if (cardName.length < 3) {
                var msgName = 'Ingresa el nombre del titular.';
                setFieldError('cardName', msgName);
                errors.push({ name: 'Name on Card', text: msgName });
              }
            }

            if (useInstallments && installmentOpts.length > 0 && !selectedInstallmentCode) {
              var msgInst = 'Selecciona el numero de cuotas.';
              setFieldError('installment', msgInst);
              errors.push({ name: 'Installments', text: msgInst });
            }

            return errors;
          };

          var submit = function () {
            var errors = validate();
            if (errors.length > 0) {
              emit(GatewayEmitStrings.Validation, errors);
              return;
            }

            var endpoint = (__props.settings.tokenizeEndpoint || '').toString();
            var gatewayGuid = (__props.settings.gatewayGuid || '').toString();

            if (!endpoint || !gatewayGuid) {
              emit(GatewayEmitStrings.Error, 'Gateway settings are incomplete (tokenize endpoint / gateway guid).');
              return;
            }

            var expiry = parseExpiry(dom.expDate.value);
            if (!expiry.valid) {
              emit(GatewayEmitStrings.Error, 'Expiration date is invalid.');
              return;
            }

            showLoading(true);

            fetch(endpoint, {
              method: 'POST',
              headers: {
                'Content-Type': 'application/json'
              },
              credentials: 'same-origin',
              body: JSON.stringify({
                gatewayGuid: gatewayGuid,
                cardNumber: getPanDigits(),
                expirationMonth: expiry.month,
                expirationYear: expiry.year,
                securityCode: (dom.cvv.value || '').trim(),
                nameOnCard: (dom.cardName.value || '').trim(),
                installmentCode: useInstallments ? (selectedInstallmentCode || '') : ''
              })
            }).then(function (response) {
              if (!response.ok) {
                return response.text().then(function (text) {
                  throw new Error(text || ('Tokenization failed. HTTP ' + response.status));
                });
              }

              return response.json();
            }).then(function (result) {
              var token = (result && (result.token || result.Token)) ? (result.token || result.Token).toString() : '';
              if (!token) {
                showLoading(false);
                emit(GatewayEmitStrings.Error, 'Gateway did not return a payment token.');
                return;
              }

              showLoading(false);
              emit(GatewayEmitStrings.Success, token);
            }).catch(function (e) {
              showLoading(false);
              emit(GatewayEmitStrings.Error, 'Error creating payment token. ' + (e && e.message ? e.message : 'Unknown error'));
            });
          };

          onSubmitPayment(submit);

          onMounted(function () {
            ensureStyleTag();
            var root = document.getElementById(controlId);
            if (!root) {
              return;
            }

            root.innerHTML = createMarkup(controlId, __props.settings.promptForNameOnCard !== false, installmentOpts);

            dom = {
              root: root,
              cardNumber: root.querySelector('#' + controlId + '-number'),
              expDate: root.querySelector('#' + controlId + '-exp'),
              cvv: root.querySelector('#' + controlId + '-cvv'),
              cardName: root.querySelector('#' + controlId + '-name'),
              brandTag: root.querySelector('[data-el="brandTag"]'),
              preview: root.querySelector('[data-el="preview"]'),
              previewBrand: root.querySelector('[data-el="previewBrand"]'),
              previewNumber: root.querySelector('[data-el="previewNumber"]'),
              previewName: root.querySelector('[data-el="previewName"]'),
              previewExp: root.querySelector('[data-el="previewExp"]'),
              cvvHint: root.querySelector('[data-el="cvvHint"]'),
              loadingOverlay: root.querySelector('[data-el="loadingOverlay"]'),
              brandIcons: root.querySelectorAll('[data-brand-icon]'),
              wraps: {
                cardNumber: root.querySelector('[data-wrap="cardNumber"]'),
                expDate: root.querySelector('[data-wrap="expDate"]'),
                cvv: root.querySelector('[data-wrap="cvv"]'),
                cardName: root.querySelector('[data-wrap="cardName"]'),
                installment: root.querySelector('[data-wrap="installment"]')
              },
              errors: {
                cardNumber: root.querySelector('[data-error="cardNumber"]'),
                expDate: root.querySelector('[data-error="expDate"]'),
                cvv: root.querySelector('[data-error="cvv"]'),
                cardName: root.querySelector('[data-error="cardName"]'),
                installment: root.querySelector('[data-error="installment"]')
              }
            };

            if (dom.cardNumber) dom.cardNumber.addEventListener('input', onCardNumberInput);
            if (dom.expDate) dom.expDate.addEventListener('input', onExpInput);
            if (dom.cvv) dom.cvv.addEventListener('input', onCvvInput);
            if (dom.cardName) dom.cardName.addEventListener('input', onNameInput);

            var installCheck = root.querySelector('#' + controlId + '-installcheck');
            var installSelect = root.querySelector('#' + controlId + '-installselect');
            var installSelectWrap = root.querySelector('[data-el="installmentSelectWrap"]');
            var surchargeInfo = root.querySelector('[data-el="surchargeInfo"]');
            var surchargeLabel = root.querySelector('[data-el="surchargeLabel"]');
            var surchargeAmount = root.querySelector('[data-el="surchargeAmount"]');

            if (installCheck) {
              installCheck.addEventListener('change', function () {
                useInstallments = installCheck.checked;
                if (installSelectWrap) {
                  installSelectWrap.style.display = useInstallments ? 'grid' : 'none';
                }
                if (!useInstallments) {
                  selectedInstallmentCode = '';
                  if (installSelect) installSelect.value = '';
                  if (surchargeInfo) surchargeInfo.style.display = 'none';
                  setFieldError('installment', '');
                }
              });
            }

            if (installSelect) {
              installSelect.addEventListener('change', function () {
                selectedInstallmentCode = installSelect.value;
                setFieldError('installment', '');
                var surcharge = 0;
                for (var k = 0; k < installmentOpts.length; k++) {
                  if (installmentOpts[k].code === selectedInstallmentCode) {
                    surcharge = installmentOpts[k].surcharge;
                    break;
                  }
                }
                if (surcharge > 0 && surchargeInfo && surchargeLabel && surchargeAmount) {
                  var amountToPay = sanitizeAmount(__props.amount);
                  var surchargeAmountValue = roundCurrency(amountToPay * (surcharge / 100));
                  var totalWithSurcharge = roundCurrency(amountToPay + surchargeAmountValue);
                  surchargeInfo.style.display = 'flex';
                  surchargeLabel.textContent = 'Recargo por cuotas (' + surcharge + '% sobre ' + formatAmount(amountToPay) + '):';
                  surchargeAmount.textContent = '+ ' + formatAmount(surchargeAmountValue) + ' (Total: ' + formatAmount(totalWithSurcharge) + ')';
                } else if (surchargeInfo) {
                  surchargeInfo.style.display = 'none';
                }
              });
            }

            updateBrandUI();
            updatePreview();
          });

          return function (_ctx, _cache) {
            return openBlock(), createElementBlock('div', { id: controlId });
          };
        }
      }));

      script.__file = 'Plugins/EpayVisanetGateway/Obsidian/epayVisanetGatewayControl.obs';
    }
  };
});
