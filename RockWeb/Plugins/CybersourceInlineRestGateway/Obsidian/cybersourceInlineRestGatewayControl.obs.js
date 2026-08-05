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
      var styleId = 'csir-gateway-control-style';

      function ensureStyleTag() {
        if (document.getElementById(styleId)) {
          return;
        }

        var style = document.createElement('style');
        style.id = styleId;
        style.type = 'text/css';
        style.textContent = "\n.csirWrap { --csir-bg:#f3f3f3; --csir-border:#d6d6d6; --csir-text:#1f2933; --csir-muted:#6b7280; --csir-danger:#c22016; --csir-radius-lg:14px; --csir-radius-md:10px; --csir-radius-pill:999px; background:var(--csir-bg); border:1px solid var(--csir-border); border-radius:var(--csir-radius-lg); color:var(--csir-text); padding:12px; }\n.csirBadge { display:inline-flex; align-items:center; border:1px solid #bdbdbd; border-radius:var(--csir-radius-pill); padding:4px 9px; font-size:11px; font-weight:700; color:#3b3b3b; letter-spacing:.04em; text-transform:uppercase; background:#fff; }\n.csirTitle { margin:8px 0 0; font-size:20px; font-weight:800; }\n.csirSubtitle { margin:4px 0 10px; font-size:13px; color:var(--csir-muted); }\n.csirCardPreview { margin-bottom:12px; border-radius:12px; border:1px solid #cecece; background:#fff; padding:10px 12px; display:grid; gap:4px; }\n.csirCardPreview.brand-visa { border-color:#bcd2ff; background:#eaf1ff; }\n.csirCardPreview.brand-mastercard { border-color:#ffd7c2; background:#fff1e8; }\n.csirCardPreview.brand-amex { border-color:#b7effa; background:#e8fbff; }\n.csirCardPreview.brand-discover { border-color:#fed7aa; background:#fff4ec; }\n.csirPreviewBrand { font-size:11px; font-weight:700; letter-spacing:.05em; text-transform:uppercase; color:#505050; }\n.csirPreviewNumber { font-size:15px; font-weight:800; letter-spacing:.04em; }\n.csirPreviewMeta { display:flex; justify-content:space-between; gap:8px; font-size:12px; color:#555; }\n.csirFields { display:grid; gap:10px; }\n.csirRow { display:grid; grid-template-columns:1fr; gap:10px; }\n.csirField { display:grid; gap:6px; }\n.csirField > span { font-size:11px; font-weight:700; letter-spacing:.05em; text-transform:uppercase; color:#4a4a4a; }\n.csirInputWrap { border:1px solid #bfbfbf; border-radius:var(--csir-radius-md); background:#fff; display:flex; align-items:center; gap:8px; padding:0 10px; }\n.csirInputWrap:focus-within { border-color:#7b7b7b; box-shadow:0 0 0 3px rgba(51,51,51,.12); }\n.csirInputWrap.isInvalid { border-color:#dc6e66; }\n.csirInput { width:100%; min-height:42px; border:0; background:transparent; padding:0; font-size:14px; box-shadow:none; outline:none; }\n.csirBrandTag { flex:0 0 auto; border-radius:var(--csir-radius-pill); border:1px solid #bfc6cf; background:#edf1f7; color:#405061; font-size:10px; font-weight:800; letter-spacing:.06em; text-transform:uppercase; padding:4px 7px; }\n.csirBrandTag.brand-visa { background:#eaf1ff; border-color:#bcd2ff; color:#1e44a8; }\n.csirBrandTag.brand-mastercard { background:#fff1e8; border-color:#ffd7c2; color:#9a3412; }\n.csirBrandTag.brand-amex { background:#e8fbff; border-color:#b7effa; color:#0f6f88; }\n.csirBrandTag.brand-discover { background:#fff4ec; border-color:#fed7aa; color:#c2410c; }\n.csirHint { font-size:12px; color:#727272; }\n.csirError { font-size:12px; color:var(--csir-danger); font-weight:600; min-height:14px; }\n@media (min-width: 600px) { .csirRow { grid-template-columns:1fr 1fr; } }\n";

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

      function createMarkup(controlId, showNameField) {
        return "<div class='csirWrap'>" +
          "<div><span class='csirBadge'>Pago seguro</span><h4 class='csirTitle'>Tarjeta de credito</h4><p class='csirSubtitle'>Ingresa los datos de la tarjeta para completar el pago.</p></div>" +
          "<div class='csirCardPreview brand-unknown' data-el='preview'>" +
            "<span class='csirPreviewBrand' data-el='previewBrand'>Tarjeta</span>" +
            "<strong class='csirPreviewNumber' data-el='previewNumber'>#### #### #### ####</strong>" +
            "<div class='csirPreviewMeta'><span data-el='previewName'>NOMBRE TITULAR</span><span data-el='previewExp'>MM/YY</span></div>" +
          "</div>" +
          "<div class='csirFields'>" +
            "<label class='csirField'>" +
              "<span>Numero de tarjeta</span>" +
              "<div class='csirInputWrap' data-wrap='cardNumber'>" +
                "<input id='" + controlId + "-number' class='csirInput' type='text' maxlength='24' inputmode='numeric' autocomplete='cc-number' placeholder='4111 1111 1111 1111' />" +
                "<span class='csirBrandTag brand-unknown' data-el='brandTag'>Tarjeta</span>" +
              "</div>" +
              "<small class='csirError' data-error='cardNumber'></small>" +
            "</label>" +
            "<div class='csirRow'>" +
              "<label class='csirField'>" +
                "<span>Vencimiento</span>" +
                "<div class='csirInputWrap' data-wrap='expDate'><input id='" + controlId + "-exp' class='csirInput' type='text' maxlength='5' inputmode='numeric' autocomplete='cc-exp' placeholder='MM/YY' /></div>" +
                "<small class='csirHint'>Formato: MM/YY</small>" +
                "<small class='csirError' data-error='expDate'></small>" +
              "</label>" +
              "<label class='csirField'>" +
                "<span>CVV</span>" +
                "<div class='csirInputWrap' data-wrap='cvv'><input id='" + controlId + "-cvv' class='csirInput' type='password' maxlength='4' inputmode='numeric' autocomplete='cc-csc' placeholder='CVV (3)' /></div>" +
                "<small class='csirHint' data-el='cvvHint'>3 digitos para Tarjeta</small>" +
                "<small class='csirError' data-error='cvv'></small>" +
              "</label>" +
            "</div>" +
            "<label class='csirField' data-el='nameField' style='display:" + (showNameField ? "grid" : "none") + ";'>" +
              "<span>Nombre del titular</span>" +
              "<div class='csirInputWrap' data-wrap='cardName'><input id='" + controlId + "-name' class='csirInput' type='text' maxlength='120' autocomplete='cc-name' placeholder='Nombre como aparece en la tarjeta' /></div>" +
              "<small class='csirError' data-error='cardName'></small>" +
            "</label>" +
          "</div>" +
        "</div>";
      }

      var script = exports('default', defineComponent({
        __name: 'cybersourceInlineRestGatewayControl',
        props: {
          settings: {
            type: Object,
            required: true
          }
        },
        setup: function (__props, _ref) {
          var emit = _ref.emit;
          var controlId = 'csir-' + newGuid().replace(/-/g, '');
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

          var clearFieldErrors = function () {
            setFieldError('cardNumber', '');
            setFieldError('expDate', '');
            setFieldError('cvv', '');
            setFieldError('cardName', '');
          };

          var updateBrandUI = function () {
            if (!dom) return;
            var brand = getBrand();
            var label = getBrandLabel(brand);
            var badgeClasses = 'csirBrandTag brand-' + brand;
            dom.brandTag.className = badgeClasses;
            dom.brandTag.textContent = label;
            dom.previewBrand.textContent = label;
            dom.preview.className = 'csirCardPreview brand-' + brand;
            dom.cvv.placeholder = 'CVV (' + expectedCvvLength() + ')';
            dom.cvvHint.textContent = expectedCvvLength() + ' digitos para ' + label;
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
                nameOnCard: (dom.cardName.value || '').trim()
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
                emit(GatewayEmitStrings.Error, 'Gateway did not return a payment token.');
                return;
              }

              emit(GatewayEmitStrings.Success, token);
            }).catch(function (e) {
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

            root.innerHTML = createMarkup(controlId, __props.settings.promptForNameOnCard !== false);

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
              wraps: {
                cardNumber: root.querySelector('[data-wrap="cardNumber"]'),
                expDate: root.querySelector('[data-wrap="expDate"]'),
                cvv: root.querySelector('[data-wrap="cvv"]'),
                cardName: root.querySelector('[data-wrap="cardName"]')
              },
              errors: {
                cardNumber: root.querySelector('[data-error="cardNumber"]'),
                expDate: root.querySelector('[data-error="expDate"]'),
                cvv: root.querySelector('[data-error="cvv"]'),
                cardName: root.querySelector('[data-error="cardName"]')
              }
            };

            if (dom.cardNumber) dom.cardNumber.addEventListener('input', onCardNumberInput);
            if (dom.expDate) dom.expDate.addEventListener('input', onExpInput);
            if (dom.cvv) dom.cvv.addEventListener('input', onCvvInput);
            if (dom.cardName) dom.cardName.addEventListener('input', onNameInput);

            updateBrandUI();
            updatePreview();
          });

          return function (_ctx, _cache) {
            return openBlock(), createElementBlock('div', { id: controlId });
          };
        }
      }));

      script.__file = 'Plugins/CybersourceInlineRestGateway/Obsidian/cybersourceInlineRestGatewayControl.obs';
    }
  };
});
