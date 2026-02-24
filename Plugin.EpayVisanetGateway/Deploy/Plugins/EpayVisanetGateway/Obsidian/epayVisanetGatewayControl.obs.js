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

      function ensureStyleTag() {
        if (document.getElementById(styleId)) {
          return;
        }

        var style = document.createElement('style');
        style.id = styleId;
        style.type = 'text/css';
        style.textContent = "\n.epayWrap { --epay-bg:#f3f3f3; --epay-border:#d6d6d6; --epay-text:#1f2933; --epay-muted:#6b7280; --epay-danger:#c22016; --epay-radius-lg:14px; --epay-radius-md:10px; --epay-radius-pill:999px; background:var(--epay-bg); border:1px solid var(--epay-border); border-radius:var(--epay-radius-lg); color:var(--epay-text); padding:12px; }\n.epayBadge { display:inline-flex; align-items:center; border:1px solid #bdbdbd; border-radius:var(--epay-radius-pill); padding:4px 9px; font-size:11px; font-weight:700; color:#3b3b3b; letter-spacing:.04em; text-transform:uppercase; background:#fff; }\n.epayTitle { margin:8px 0 0; font-size:20px; font-weight:800; }\n.epaySubtitle { margin:4px 0 10px; font-size:13px; color:var(--epay-muted); }\n.epayCardPreview { margin-bottom:12px; border-radius:12px; border:1px solid #cecece; background:#fff; padding:10px 12px; display:grid; gap:4px; }\n.epayCardPreview.brand-visa { border-color:#bcd2ff; background:#eaf1ff; }\n.epayCardPreview.brand-mastercard { border-color:#ffd7c2; background:#fff1e8; }\n.epayCardPreview.brand-amex { border-color:#b7effa; background:#e8fbff; }\n.epayCardPreview.brand-discover { border-color:#fed7aa; background:#fff4ec; }\n.epayPreviewBrand { font-size:11px; font-weight:700; letter-spacing:.05em; text-transform:uppercase; color:#505050; }\n.epayPreviewNumber { font-size:15px; font-weight:800; letter-spacing:.04em; }\n.epayPreviewMeta { display:flex; justify-content:space-between; gap:8px; font-size:12px; color:#555; }\n.epayFields { display:grid; gap:10px; }\n.epayRow { display:grid; grid-template-columns:1fr; gap:10px; }\n.epayField { display:grid; gap:6px; }\n.epayField > span { font-size:11px; font-weight:700; letter-spacing:.05em; text-transform:uppercase; color:#4a4a4a; }\n.epayInputWrap { border:1px solid #bfbfbf; border-radius:var(--epay-radius-md); background:#fff; display:flex; align-items:center; gap:8px; padding:0 10px; }\n.epayInputWrap:focus-within { border-color:#7b7b7b; box-shadow:0 0 0 3px rgba(51,51,51,.12); }\n.epayInputWrap.isInvalid { border-color:#dc6e66; }\n.epayInput { width:100%; min-height:42px; border:0; background:transparent; padding:0; font-size:14px; box-shadow:none; outline:none; }\n.epayBrandTag { flex:0 0 auto; border-radius:var(--epay-radius-pill); border:1px solid #bfc6cf; background:#edf1f7; color:#405061; font-size:10px; font-weight:800; letter-spacing:.06em; text-transform:uppercase; padding:4px 7px; }\n.epayBrandTag.brand-visa { background:#eaf1ff; border-color:#bcd2ff; color:#1e44a8; }\n.epayBrandTag.brand-mastercard { background:#fff1e8; border-color:#ffd7c2; color:#9a3412; }\n.epayBrandTag.brand-amex { background:#e8fbff; border-color:#b7effa; color:#0f6f88; }\n.epayBrandTag.brand-discover { background:#fff4ec; border-color:#fed7aa; color:#c2410c; }\n.epayHint { font-size:12px; color:#727272; }\n.epayError { font-size:12px; color:var(--epay-danger); font-weight:600; min-height:14px; }\n.epayInstallments { border-top:1px solid var(--epay-border); padding-top:10px; display:grid; gap:10px; }\n.epayCheckboxLabel { display:flex; align-items:center; gap:8px; font-size:14px; font-weight:600; cursor:pointer; }\n.epayCheckboxLabel input[type='checkbox'] { width:18px; height:18px; accent-color:#1e44a8; cursor:pointer; }\n.epayInstallmentSelect { display:grid; gap:10px; }\n.epayInputWrap select.epayInput { appearance:auto; cursor:pointer; }\n.epaySurchargeInfo { display:flex; justify-content:space-between; align-items:center; background:#fff8e1; border:1px solid #ffe082; border-radius:var(--epay-radius-md); padding:8px 12px; }\n.epaySurchargeLabel { font-size:13px; color:#6d5c00; }\n.epaySurchargeAmount { font-size:14px; font-weight:800; color:#b8860b; }\n@media (min-width: 600px) { .epayRow { grid-template-columns:1fr 1fr; } }\n";

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

      function createMarkup(controlId, showNameField, installmentOpts) {
        var html = "<div class='epayWrap'>" +
          "<div><span class='epayBadge'>Pago seguro</span><h4 class='epayTitle'>Tarjeta de credito</h4><p class='epaySubtitle'>Ingresa los datos de la tarjeta para completar el pago.</p></div>" +
          "<div class='epayCardPreview brand-unknown' data-el='preview'>" +
            "<span class='epayPreviewBrand' data-el='previewBrand'>Tarjeta</span>" +
            "<strong class='epayPreviewNumber' data-el='previewNumber'>#### #### #### ####</strong>" +
            "<div class='epayPreviewMeta'><span data-el='previewName'>NOMBRE TITULAR</span><span data-el='previewExp'>MM/YY</span></div>" +
          "</div>" +
          "<div class='epayFields'>" +
            "<label class='epayField'>" +
              "<span>Numero de tarjeta</span>" +
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
            var badgeClasses = 'epayBrandTag brand-' + brand;
            dom.brandTag.className = badgeClasses;
            dom.brandTag.textContent = label;
            dom.previewBrand.textContent = label;
            dom.preview.className = 'epayCardPreview brand-' + brand;
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
