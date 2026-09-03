System.register(['vue', '@Obsidian/Controls/panel.obs', '@Obsidian/Controls/rockButton.obs', '@Obsidian/Controls/textBox.obs', '@Obsidian/Controls/checkBox.obs', '@Obsidian/Controls/dropDownList.obs', '@Obsidian/Controls/modal.obs', '@Obsidian/Utility/block', '@Obsidian/Utility/dialogs'], (function (exports) {
  'use strict';
  var createElementVNode, createTextVNode, defineComponent, reactive, ref, computed, openBlock, createBlock, unref, withCtx, createCommentVNode, createElementBlock, toDisplayString, normalizeClass, normalizeStyle, createVNode, withDirectives, vModelText, Fragment, renderList, withKeys, Panel, RockButton, TextBox, CheckBox, DropDownList, Modal, useConfigurationValues, useInvokeBlockAction, confirm;
  return {
    setters: [function (module) {
      createElementVNode = module.createElementVNode;
      createTextVNode = module.createTextVNode;
      defineComponent = module.defineComponent;
      reactive = module.reactive;
      ref = module.ref;
      computed = module.computed;
      openBlock = module.openBlock;
      createBlock = module.createBlock;
      unref = module.unref;
      withCtx = module.withCtx;
      createCommentVNode = module.createCommentVNode;
      createElementBlock = module.createElementBlock;
      toDisplayString = module.toDisplayString;
      normalizeClass = module.normalizeClass;
      normalizeStyle = module.normalizeStyle;
      createVNode = module.createVNode;
      withDirectives = module.withDirectives;
      vModelText = module.vModelText;
      Fragment = module.Fragment;
      renderList = module.renderList;
      withKeys = module.withKeys;
    }, function (module) {
      Panel = module.default;
    }, function (module) {
      RockButton = module.default;
    }, function (module) {
      TextBox = module.default;
    }, function (module) {
      CheckBox = module.default;
    }, function (module) {
      DropDownList = module.default;
    }, function (module) {
      Modal = module.default;
    }, function (module) {
      useConfigurationValues = module.useConfigurationValues;
      useInvokeBlockAction = module.useInvokeBlockAction;
    }, function (module) {
      confirm = module.confirm;
    }],
    execute: (function () {

      function ownKeys(e, r) {
        var t = Object.keys(e);
        if (Object.getOwnPropertySymbols) {
          var o = Object.getOwnPropertySymbols(e);
          r && (o = o.filter(function (r) {
            return Object.getOwnPropertyDescriptor(e, r).enumerable;
          })), t.push.apply(t, o);
        }
        return t;
      }
      function _objectSpread2(e) {
        for (var r = 1; r < arguments.length; r++) {
          var t = null != arguments[r] ? arguments[r] : {};
          r % 2 ? ownKeys(Object(t), !0).forEach(function (r) {
            _defineProperty(e, r, t[r]);
          }) : Object.getOwnPropertyDescriptors ? Object.defineProperties(e, Object.getOwnPropertyDescriptors(t)) : ownKeys(Object(t)).forEach(function (r) {
            Object.defineProperty(e, r, Object.getOwnPropertyDescriptor(t, r));
          });
        }
        return e;
      }
      function asyncGeneratorStep(gen, resolve, reject, _next, _throw, key, arg) {
        try {
          var info = gen[key](arg);
          var value = info.value;
        } catch (error) {
          reject(error);
          return;
        }
        if (info.done) {
          resolve(value);
        } else {
          Promise.resolve(value).then(_next, _throw);
        }
      }
      function _asyncToGenerator(fn) {
        return function () {
          var self = this,
            args = arguments;
          return new Promise(function (resolve, reject) {
            var gen = fn.apply(self, args);
            function _next(value) {
              asyncGeneratorStep(gen, resolve, reject, _next, _throw, "next", value);
            }
            function _throw(err) {
              asyncGeneratorStep(gen, resolve, reject, _next, _throw, "throw", err);
            }
            _next(undefined);
          });
        };
      }
      function _defineProperty(obj, key, value) {
        key = _toPropertyKey(key);
        if (key in obj) {
          Object.defineProperty(obj, key, {
            value: value,
            enumerable: true,
            configurable: true,
            writable: true
          });
        } else {
          obj[key] = value;
        }
        return obj;
      }
      function _toPrimitive(input, hint) {
        if (typeof input !== "object" || input === null) return input;
        var prim = input[Symbol.toPrimitive];
        if (prim !== undefined) {
          var res = prim.call(input, hint || "default");
          if (typeof res !== "object") return res;
          throw new TypeError("@@toPrimitive must return a primitive value.");
        }
        return (hint === "string" ? String : Number)(input);
      }
      function _toPropertyKey(arg) {
        var key = _toPrimitive(arg, "string");
        return typeof key === "symbol" ? key : String(key);
      }

      var _hoisted_1 = {
        class: "vtWrap notranslate",
        "data-no-translate": "1"
      };
      var _hoisted_2 = {
        key: 0,
        class: "vtToast vtToast--err",
        role: "alert"
      };
      var _hoisted_3 = createElementVNode("i", {
        class: "fa fa-exclamation-circle"
      }, null, -1);
      var _hoisted_4 = {
        key: 1,
        class: "vtToast vtToast--ok",
        role: "status"
      };
      var _hoisted_5 = createElementVNode("i", {
        class: "fa fa-check-circle"
      }, null, -1);
      var _hoisted_6 = {
        class: "vtBar"
      };
      var _hoisted_7 = createElementVNode("div", null, [createElementVNode("h1", {
        class: "vtTitle"
      }, [createElementVNode("i", {
        class: "fa fa-language"
      }), createTextVNode(" VidaReal Translator")]), createElementVNode("p", {
        class: "vtSub"
      }, "Traducción de la UI en tiempo real, con caché en BD y Azure OpenAI.")], -1);
      var _hoisted_8 = {
        class: "vtBarRight"
      };
      var _hoisted_9 = {
        class: "vtVer vtMono"
      };
      var _hoisted_10 = {
        class: "vtSwitch"
      };
      var _hoisted_11 = ["checked", "disabled"];
      var _hoisted_12 = createElementVNode("span", {
        class: "vtSwitchTrack"
      }, [createElementVNode("span", {
        class: "vtSwitchThumb"
      })], -1);
      var _hoisted_13 = {
        class: "vtCards"
      };
      var _hoisted_14 = {
        class: "vtCard"
      };
      var _hoisted_15 = createElementVNode("span", {
        class: "vtCardIcon vtIco--indigo"
      }, [createElementVNode("i", {
        class: "fa fa-database"
      })], -1);
      var _hoisted_16 = {
        class: "vtCardBody"
      };
      var _hoisted_17 = {
        class: "vtCardNum"
      };
      var _hoisted_18 = createElementVNode("span", {
        class: "vtCardLbl"
      }, "traducciones en caché", -1);
      var _hoisted_19 = {
        class: "vtCardHint"
      };
      var _hoisted_20 = {
        class: "vtCard"
      };
      var _hoisted_21 = createElementVNode("span", {
        class: "vtCardIcon vtIco--amber"
      }, [createElementVNode("i", {
        class: "fa fa-tachometer-alt"
      })], -1);
      var _hoisted_22 = {
        class: "vtCardBody"
      };
      var _hoisted_23 = {
        class: "vtCardNum"
      };
      var _hoisted_24 = createElementVNode("span", {
        class: "vtCardLbl"
      }, "traducciones nuevas esta hora", -1);
      var _hoisted_25 = {
        class: "vtMeter"
      };
      var _hoisted_26 = {
        class: "vtCard"
      };
      var _hoisted_27 = createElementVNode("span", {
        class: "vtCardIcon vtIco--teal"
      }, [createElementVNode("i", {
        class: "fa fa-sitemap"
      })], -1);
      var _hoisted_28 = {
        class: "vtCardBody"
      };
      var _hoisted_29 = {
        class: "vtCardNum"
      };
      var _hoisted_30 = createElementVNode("span", {
        class: "vtCardLbl"
      }, "sitios con el script", -1);
      var _hoisted_31 = {
        key: 0,
        class: "vtCardHint vtCardHint--warn"
      };
      var _hoisted_32 = createElementVNode("i", {
        class: "fa fa-exclamation-triangle"
      }, null, -1);
      var _hoisted_33 = {
        key: 1,
        class: "vtCardHint"
      };
      var _hoisted_34 = {
        class: "vtCard"
      };
      var _hoisted_35 = createElementVNode("span", {
        class: "vtCardIcon vtIco--sky"
      }, [createElementVNode("i", {
        class: "fa fa-cloud"
      })], -1);
      var _hoisted_36 = {
        class: "vtCardBody"
      };
      var _hoisted_37 = createElementVNode("span", {
        class: "vtCardNum vtCardNum--sm"
      }, "Azure OpenAI", -1);
      var _hoisted_38 = {
        class: "vtCardLbl"
      };
      var _hoisted_39 = ["disabled"];
      var _hoisted_40 = {
        class: "vtSec"
      };
      var _hoisted_41 = {
        class: "vtSecHead"
      };
      var _hoisted_42 = createElementVNode("h2", {
        class: "vtSecTitle"
      }, [createElementVNode("i", {
        class: "fa fa-sliders-h"
      }), createTextVNode(" Configuración")], -1);
      var _hoisted_43 = {
        class: "row"
      };
      var _hoisted_44 = {
        class: "col-md-6"
      };
      var _hoisted_45 = createElementVNode("h3", {
        class: "vtSubHead"
      }, "Idiomas", -1);
      var _hoisted_46 = {
        class: "row"
      };
      var _hoisted_47 = {
        class: "col-sm-6"
      };
      var _hoisted_48 = {
        class: "col-sm-6"
      };
      var _hoisted_49 = createElementVNode("h3", {
        class: "vtSubHead"
      }, "Selector de idioma (switcher)", -1);
      var _hoisted_50 = {
        class: "col-md-6"
      };
      var _hoisted_51 = createElementVNode("h3", {
        class: "vtSubHead"
      }, "Azure OpenAI", -1);
      var _hoisted_52 = {
        class: "row"
      };
      var _hoisted_53 = {
        class: "col-sm-6"
      };
      var _hoisted_54 = {
        class: "col-sm-6"
      };
      var _hoisted_55 = {
        class: "form-group"
      };
      var _hoisted_56 = {
        class: "control-label"
      };
      var _hoisted_57 = ["placeholder"];
      var _hoisted_58 = createElementVNode("h3", {
        class: "vtSubHead"
      }, "Selectores avanzados", -1);
      var _hoisted_59 = {
        class: "vtSec"
      };
      var _hoisted_60 = {
        class: "vtSecHead"
      };
      var _hoisted_61 = createElementVNode("h2", {
        class: "vtSecTitle"
      }, [createElementVNode("i", {
        class: "fa fa-globe-americas"
      }), createTextVNode(" Caché por idioma")], -1);
      var _hoisted_62 = {
        key: 0,
        class: "vtSecBtns"
      };
      var _hoisted_63 = createElementVNode("i", {
        class: "fa fa-sync-alt"
      }, null, -1);
      var _hoisted_64 = createElementVNode("i", {
        class: "fa fa-syringe"
      }, null, -1);
      var _hoisted_65 = createElementVNode("i", {
        class: "fa fa-trash-alt"
      }, null, -1);
      var _hoisted_66 = {
        key: 0,
        class: "vtEmpty"
      };
      var _hoisted_67 = createElementVNode("i", {
        class: "fa fa-inbox"
      }, null, -1);
      var _hoisted_68 = createElementVNode("p", null, "Aún no hay traducciones en caché.", -1);
      var _hoisted_69 = [_hoisted_67, _hoisted_68];
      var _hoisted_70 = {
        key: 1,
        class: "vtScroll"
      };
      var _hoisted_71 = {
        class: "vtTable"
      };
      var _hoisted_72 = createElementVNode("th", null, "Idioma", -1);
      var _hoisted_73 = createElementVNode("th", {
        class: "vtNum"
      }, "Total", -1);
      var _hoisted_74 = createElementVNode("th", {
        class: "vtNum"
      }, "Traducidas", -1);
      var _hoisted_75 = createElementVNode("th", {
        class: "vtNum"
      }, "Excluidas", -1);
      var _hoisted_76 = createElementVNode("th", null, "Última actividad", -1);
      var _hoisted_77 = {
        key: 0
      };
      var _hoisted_78 = {
        class: "vtPill vtMono"
      };
      var _hoisted_79 = {
        class: "vtNum"
      };
      var _hoisted_80 = {
        class: "vtNum"
      };
      var _hoisted_81 = {
        class: "vtNum"
      };
      var _hoisted_82 = {
        class: "vtMuted vtMono"
      };
      var _hoisted_83 = {
        key: 0,
        class: "vtRowActions"
      };
      var _hoisted_84 = ["disabled", "onClick"];
      var _hoisted_85 = createElementVNode("i", {
        class: "fa fa-trash-alt"
      }, null, -1);
      var _hoisted_86 = {
        class: "vtScroll vtSites"
      };
      var _hoisted_87 = {
        class: "vtTable vtTable--tight"
      };
      var _hoisted_88 = createElementVNode("thead", null, [createElementVNode("tr", null, [createElementVNode("th", null, "Sitio"), createElementVNode("th", null, "Script")])], -1);
      var _hoisted_89 = {
        key: 0,
        class: "vtPill vtPill--off"
      };
      var _hoisted_90 = {
        key: 1,
        class: "vtPill vtPill--warn vtMono"
      };
      var _hoisted_91 = {
        key: 2,
        class: "vtPill vtPill--ok vtMono"
      };
      var _hoisted_92 = {
        class: "vtSec"
      };
      var _hoisted_93 = {
        class: "vtSecHead"
      };
      var _hoisted_94 = createElementVNode("h2", {
        class: "vtSecTitle"
      }, [createElementVNode("i", {
        class: "fa fa-list"
      }), createTextVNode(" Traducciones")], -1);
      var _hoisted_95 = {
        class: "vtMuted"
      };
      var _hoisted_96 = {
        class: "vtFilters"
      };
      var _hoisted_97 = {
        class: "vtFilterGrow"
      };
      var _hoisted_98 = createElementVNode("i", {
        class: "fa fa-search"
      }, null, -1);
      var _hoisted_99 = {
        key: 0,
        class: "vtEmpty"
      };
      var _hoisted_100 = createElementVNode("i", {
        class: "fa fa-spinner fa-spin"
      }, null, -1);
      var _hoisted_101 = createElementVNode("p", null, "Cargando…", -1);
      var _hoisted_102 = [_hoisted_100, _hoisted_101];
      var _hoisted_103 = {
        key: 1,
        class: "vtEmpty"
      };
      var _hoisted_104 = createElementVNode("i", {
        class: "fa fa-search"
      }, null, -1);
      var _hoisted_105 = createElementVNode("p", null, "Sin resultados con esos filtros.", -1);
      var _hoisted_106 = [_hoisted_104, _hoisted_105];
      var _hoisted_107 = {
        key: 2,
        class: "vtScroll"
      };
      var _hoisted_108 = {
        class: "vtTable vtTable--rows"
      };
      var _hoisted_109 = createElementVNode("thead", null, [createElementVNode("tr", null, [createElementVNode("th", null, "Original"), createElementVNode("th", null, "Traducción"), createElementVNode("th", null, "Idioma"), createElementVNode("th", null, "Status"), createElementVNode("th", null, "Modificada")])], -1);
      var _hoisted_110 = ["onClick"];
      var _hoisted_111 = {
        class: "vtCell"
      };
      var _hoisted_112 = {
        class: "vtCell"
      };
      var _hoisted_113 = {
        class: "vtPill vtMono"
      };
      var _hoisted_114 = {
        class: "vtMuted vtMono"
      };
      var _hoisted_115 = {
        key: 3,
        class: "vtPager"
      };
      var _hoisted_116 = ["disabled"];
      var _hoisted_117 = createElementVNode("i", {
        class: "fa fa-chevron-left"
      }, null, -1);
      var _hoisted_118 = [_hoisted_117];
      var _hoisted_119 = {
        class: "vtMuted"
      };
      var _hoisted_120 = ["disabled"];
      var _hoisted_121 = createElementVNode("i", {
        class: "fa fa-chevron-right"
      }, null, -1);
      var _hoisted_122 = [_hoisted_121];
      var _hoisted_123 = {
        key: 0,
        class: "notranslate",
        "data-no-translate": "1"
      };
      var _hoisted_124 = {
        class: "vtEditSrc"
      };
      var _hoisted_125 = {
        class: "vtEditLbl"
      };
      var _hoisted_126 = ["disabled"];
      var _hoisted_127 = createElementVNode("i", {
        class: "fa fa-trash-alt"
      }, null, -1);
      var PAGE_SIZE = 50;
      var script = exports('default', defineComponent({
        __name: 'translatorDashboard',
        setup(__props) {
          var _config$stats, _config$sites;
          var config = useConfigurationValues();
          var invokeBlockAction = useInvokeBlockAction();
          var canEdit = config.canEdit;
          var status = reactive({
            enabled: config.enabled,
            scriptVersion: config.scriptVersion,
            stats: (_config$stats = config.stats) !== null && _config$stats !== void 0 ? _config$stats : [],
            sites: (_config$sites = config.sites) !== null && _config$sites !== void 0 ? _config$sites : [],
            throttleUsed: config.throttleUsed,
            throttleLimit: config.throttleLimit
          });
          var settings = reactive(_objectSpread2(_objectSpread2({}, config.settings), {}, {
            newApiKey: ""
          }));
          var ok = ref("");
          var err = ref("");
          var busy = reactive({
            enabled: false,
            save: false,
            test: false,
            reinject: false,
            refresh: false,
            purge: false,
            grid: false,
            edit: false
          });
          var testResult = ref(null);
          var totalTranslations = computed(() => status.stats.reduce((acc, s) => acc + s.total, 0));
          var injectedCount = computed(() => status.sites.filter(s => s.isInjected).length);
          var staleCount = computed(() => status.sites.filter(s => s.isStale).length);
          var throttlePct = computed(() => status.throttleLimit > 0 ? Math.min(100, Math.round(status.throttleUsed * 100 / status.throttleLimit)) : 0);
          function toast(kind, msg) {
            if (kind === "ok") {
              ok.value = msg;
              window.setTimeout(() => {
                if (ok.value === msg) ok.value = "";
              }, 4000);
            } else {
              err.value = msg;
            }
          }
          function statusLabel(s) {
            return s === "Excluded" ? "Excluida" : "Traducida";
          }
          function toggleEnabled(_x) {
            return _toggleEnabled.apply(this, arguments);
          }
          function _toggleEnabled() {
            _toggleEnabled = _asyncToGenerator(function* (enabled) {
              busy.enabled = true;
              var result = yield invokeBlockAction("SetEnabled", {
                enabled
              });
              busy.enabled = false;
              if (result.isSuccess && result.data) {
                Object.assign(status, result.data);
                toast("ok", enabled ? "Traductor activado e inyectado en todos los sitios." : "Traductor desactivado y retirado de todos los sitios.");
              } else {
                toast("err", result.errorMessage || "No se pudo cambiar el estado.");
              }
            });
            return _toggleEnabled.apply(this, arguments);
          }
          function saveSettings() {
            return _saveSettings.apply(this, arguments);
          }
          function _saveSettings() {
            _saveSettings = _asyncToGenerator(function* () {
              busy.save = true;
              var result = yield invokeBlockAction("SaveSettings", {
                settings: _objectSpread2({}, settings)
              });
              busy.save = false;
              if (result.isSuccess) {
                if (settings.newApiKey) {
                  settings.hasApiKey = true;
                }
                settings.newApiKey = "";
                toast("ok", "Configuración guardada.");
              } else {
                toast("err", result.errorMessage || "No se pudo guardar la configuración.");
              }
            });
            return _saveSettings.apply(this, arguments);
          }
          function testConnection() {
            return _testConnection.apply(this, arguments);
          }
          function _testConnection() {
            _testConnection = _asyncToGenerator(function* () {
              busy.test = true;
              testResult.value = null;
              var result = yield invokeBlockAction("TestConnection", {});
              busy.test = false;
              testResult.value = result.isSuccess && result.data ? result.data : {
                success: false,
                message: result.errorMessage || "No se pudo probar la conexión."
              };
            });
            return _testConnection.apply(this, arguments);
          }
          function reinject() {
            return _reinject.apply(this, arguments);
          }
          function _reinject() {
            _reinject = _asyncToGenerator(function* () {
              busy.reinject = true;
              var result = yield invokeBlockAction("Reinject", {});
              busy.reinject = false;
              if (result.isSuccess && result.data) {
                Object.assign(status, result.data);
                toast("ok", "Script re-inyectado (v" + status.scriptVersion + ") en todos los sitios.");
              } else {
                toast("err", result.errorMessage || "No se pudo re-inyectar.");
              }
            });
            return _reinject.apply(this, arguments);
          }
          function refreshBrowsers() {
            return _refreshBrowsers.apply(this, arguments);
          }
          function _refreshBrowsers() {
            _refreshBrowsers = _asyncToGenerator(function* () {
              if (!(yield confirm("Todos los navegadores limpiarán su caché local en la próxima carga y la reconstruirán desde la BD. ¿Continuar?"))) {
                return;
              }
              busy.refresh = true;
              var result = yield invokeBlockAction("RefreshBrowsers", {});
              busy.refresh = false;
              if (result.isSuccess) {
                toast("ok", "Listo: los navegadores refrescarán su caché local.");
              } else {
                toast("err", result.errorMessage || "No se pudo.");
              }
            });
            return _refreshBrowsers.apply(this, arguments);
          }
          function purge(_x2) {
            return _purge.apply(this, arguments);
          }
          function _purge() {
            _purge = _asyncToGenerator(function* (language) {
              var scope = language ? "las traducciones de \"".concat(language, "\"") : "TODAS las traducciones";
              if (!(yield confirm("Se eliminar\xE1n ".concat(scope, " de la BD y la IA las regenerar\xE1 (con costo) conforme se naveguen las p\xE1ginas. \xBFContinuar?")))) {
                return;
              }
              busy.purge = true;
              var result = yield invokeBlockAction("Purge", {
                language
              });
              busy.purge = false;
              if (result.isSuccess && result.data) {
                Object.assign(status, result.data.status);
                toast("ok", "Cach\xE9 purgada: ".concat(result.data.deleted.toLocaleString(), " traducciones eliminadas."));
                loadGrid();
              } else {
                toast("err", result.errorMessage || "No se pudo purgar.");
              }
            });
            return _purge.apply(this, arguments);
          }
          var filter = reactive({
            search: "",
            language: "",
            status: "",
            page: 0
          });
          var grid = reactive({
            total: 0,
            rows: []
          });
          var pageCount = computed(() => Math.ceil(grid.total / PAGE_SIZE));
          var langItems = computed(() => [{
            value: "",
            text: "Todos los idiomas"
          }, ...status.stats.map(s => ({
            value: s.language,
            text: s.language
          }))]);
          var statusItems = [{
            value: "",
            text: "Todos los status"
          }, {
            value: "Translated",
            text: "Traducidas"
          }, {
            value: "Excluded",
            text: "Excluidas"
          }];
          function loadGrid() {
            return _loadGrid.apply(this, arguments);
          }
          function _loadGrid() {
            _loadGrid = _asyncToGenerator(function* () {
              busy.grid = true;
              var result = yield invokeBlockAction("GetTranslations", {
                language: filter.language,
                status: filter.status,
                search: filter.search,
                page: filter.page,
                pageSize: PAGE_SIZE
              });
              busy.grid = false;
              if (result.isSuccess && result.data) {
                grid.total = result.data.total;
                grid.rows = result.data.rows;
              } else {
                toast("err", result.errorMessage || "No se pudieron cargar las traducciones.");
              }
            });
            return _loadGrid.apply(this, arguments);
          }
          function search() {
            filter.page = 0;
            loadGrid();
          }
          function goPage(p) {
            filter.page = p;
            loadGrid();
          }
          var editOpen = ref(false);
          var editRow = ref(null);
          var editText = ref("");
          var editStatus = ref("Translated");
          var editStatusItems = [{
            value: "Translated",
            text: "Traducida"
          }, {
            value: "Excluded",
            text: "Excluida (dejar original)"
          }];
          function openEdit(row) {
            editRow.value = row;
            editText.value = row.translatedText || "";
            editStatus.value = row.status === "Excluded" ? "Excluded" : "Translated";
            editOpen.value = true;
          }
          function saveEdit() {
            return _saveEdit.apply(this, arguments);
          }
          function _saveEdit() {
            _saveEdit = _asyncToGenerator(function* () {
              if (!editRow.value) {
                return;
              }
              busy.edit = true;
              var result = yield invokeBlockAction("SaveTranslation", {
                id: editRow.value.id,
                translatedText: editText.value,
                status: editStatus.value
              });
              busy.edit = false;
              if (result.isSuccess) {
                editOpen.value = false;
                toast("ok", "Traducción actualizada; los navegadores la refrescan en la próxima carga.");
                loadGrid();
              } else {
                toast("err", result.errorMessage || "No se pudo guardar.");
              }
            });
            return _saveEdit.apply(this, arguments);
          }
          function deleteEdit() {
            return _deleteEdit.apply(this, arguments);
          }
          function _deleteEdit() {
            _deleteEdit = _asyncToGenerator(function* () {
              if (!editRow.value) {
                return;
              }
              if (!(yield confirm("¿Eliminar esta traducción de la caché?"))) {
                return;
              }
              busy.edit = true;
              var result = yield invokeBlockAction("DeleteTranslation", {
                id: editRow.value.id
              });
              busy.edit = false;
              if (result.isSuccess) {
                editOpen.value = false;
                toast("ok", "Traducción eliminada.");
                loadGrid();
              } else {
                toast("err", result.errorMessage || "No se pudo eliminar.");
              }
            });
            return _deleteEdit.apply(this, arguments);
          }
          loadGrid();
          return (_ctx, _cache) => {
            return openBlock(), createBlock(unref(Panel), {
              type: "block"
            }, {
              default: withCtx(() => [createCommentVNode(" notranslate: la pagina admin del propio traductor NO se traduce (el grid muestra\n                 los textos originales; traducirlos en el DOM mutaria justo lo que se esta revisando). "), createElementVNode("div", _hoisted_1, [err.value ? (openBlock(), createElementBlock("div", _hoisted_2, [_hoisted_3, createElementVNode("span", null, toDisplayString(err.value), 1), createElementVNode("button", {
                type: "button",
                class: "vtToastX",
                "aria-label": "Cerrar",
                onClick: _cache[0] || (_cache[0] = $event => err.value = '')
              }, "×")])) : createCommentVNode("v-if", true), ok.value ? (openBlock(), createElementBlock("div", _hoisted_4, [_hoisted_5, createElementVNode("span", null, toDisplayString(ok.value), 1)])) : createCommentVNode("v-if", true), createCommentVNode(" ===== Header ===== "), createElementVNode("div", _hoisted_6, [_hoisted_7, createElementVNode("div", _hoisted_8, [createElementVNode("span", _hoisted_9, "script v" + toDisplayString(status.scriptVersion), 1), createElementVNode("label", _hoisted_10, [createElementVNode("input", {
                type: "checkbox",
                checked: status.enabled,
                disabled: !unref(canEdit) || busy.enabled,
                onChange: _cache[1] || (_cache[1] = $event => toggleEnabled($event.target.checked))
              }, null, 40, _hoisted_11), _hoisted_12, createElementVNode("span", {
                class: normalizeClass(["vtSwitchLbl", status.enabled ? 'is-on' : ''])
              }, toDisplayString(status.enabled ? "Activo" : "Inactivo"), 3)])])]), createCommentVNode(" ===== Tarjetas de estado ===== "), createElementVNode("div", _hoisted_13, [createElementVNode("div", _hoisted_14, [_hoisted_15, createElementVNode("div", _hoisted_16, [createElementVNode("span", _hoisted_17, toDisplayString(totalTranslations.value.toLocaleString()), 1), _hoisted_18, createElementVNode("span", _hoisted_19, toDisplayString(status.stats.length) + " idioma" + toDisplayString(status.stats.length === 1 ? "" : "s"), 1)])]), createElementVNode("div", _hoisted_20, [_hoisted_21, createElementVNode("div", _hoisted_22, [createElementVNode("span", _hoisted_23, [createTextVNode(toDisplayString(status.throttleUsed.toLocaleString()), 1), createElementVNode("small", null, " / " + toDisplayString(status.throttleLimit.toLocaleString()), 1)]), _hoisted_24, createElementVNode("span", _hoisted_25, [createElementVNode("span", {
                class: normalizeClass(["vtMeterFill", throttlePct.value > 80 ? 'is-hot' : '']),
                style: normalizeStyle({
                  width: throttlePct.value + '%'
                })
              }, null, 6)])])]), createElementVNode("div", _hoisted_26, [_hoisted_27, createElementVNode("div", _hoisted_28, [createElementVNode("span", _hoisted_29, [createTextVNode(toDisplayString(injectedCount.value), 1), createElementVNode("small", null, " / " + toDisplayString(status.sites.length), 1)]), _hoisted_30, staleCount.value > 0 ? (openBlock(), createElementBlock("span", _hoisted_31, [_hoisted_32, createTextVNode(" " + toDisplayString(staleCount.value) + " con versión vieja", 1)])) : (openBlock(), createElementBlock("span", _hoisted_33, "todos al día"))])]), createElementVNode("div", _hoisted_34, [_hoisted_35, createElementVNode("div", _hoisted_36, [_hoisted_37, createElementVNode("span", _hoisted_38, toDisplayString(settings.hasApiKey && settings.azureEndpoint ? "configurado" : "sin configurar"), 1), createElementVNode("button", {
                type: "button",
                class: "vtLink",
                disabled: busy.test,
                onClick: testConnection
              }, [createElementVNode("i", {
                class: normalizeClass(["fa", busy.test ? 'fa-spinner fa-spin' : 'fa-stethoscope'])
              }, null, 2), createTextVNode(" " + toDisplayString(busy.test ? "Probando…" : "Probar conexión"), 1)], 8, _hoisted_39)])])]), testResult.value ? (openBlock(), createElementBlock("div", {
                key: 2,
                class: normalizeClass(["vtTest", testResult.value.success ? 'vtTest--ok' : 'vtTest--err'])
              }, [createElementVNode("i", {
                class: normalizeClass(["fa", testResult.value.success ? 'fa-check-circle' : 'fa-times-circle'])
              }, null, 2), createTextVNode(" " + toDisplayString(testResult.value.message), 1)], 2)) : createCommentVNode("v-if", true), createCommentVNode(" ===== Configuracion ===== "), createElementVNode("section", _hoisted_40, [createElementVNode("div", _hoisted_41, [_hoisted_42, unref(canEdit) ? (openBlock(), createBlock(unref(RockButton), {
                key: 0,
                btnType: "primary",
                class: "vtX",
                disabled: busy.save,
                onClick: saveSettings
              }, {
                default: withCtx(() => [createTextVNode(toDisplayString(busy.save ? "Guardando…" : "Guardar configuración"), 1)]),
                _: 1
              }, 8, ["disabled"])) : createCommentVNode("v-if", true)]), createElementVNode("div", _hoisted_43, [createElementVNode("div", _hoisted_44, [_hoisted_45, createElementVNode("div", _hoisted_46, [createElementVNode("div", _hoisted_47, [createVNode(unref(TextBox), {
                modelValue: settings.targetLanguage,
                "onUpdate:modelValue": _cache[2] || (_cache[2] = $event => settings.targetLanguage = $event),
                label: "Idioma destino (ISO)",
                help: "Idioma por defecto al que se traduce la UI, p.ej. es"
              }, null, 8, ["modelValue"])]), createElementVNode("div", _hoisted_48, [createVNode(unref(TextBox), {
                modelValue: settings.sourceLanguage,
                "onUpdate:modelValue": _cache[3] || (_cache[3] = $event => settings.sourceLanguage = $event),
                label: "Idioma original (ISO)",
                help: "Idioma en que está la UI; al elegirlo en el switcher no se traduce"
              }, null, 8, ["modelValue"])])]), _hoisted_49, createVNode(unref(CheckBox), {
                modelValue: settings.showSwitcher,
                "onUpdate:modelValue": _cache[4] || (_cache[4] = $event => settings.showSwitcher = $event),
                label: "Mostrar el selector flotante en todas las páginas"
              }, null, 8, ["modelValue"]), createVNode(unref(TextBox), {
                modelValue: settings.availableLanguages,
                "onUpdate:modelValue": _cache[5] || (_cache[5] = $event => settings.availableLanguages = $event),
                label: "Idiomas disponibles",
                textMode: "multiline",
                rows: 3,
                help: "Uno por línea, formato codigo|Etiqueta. Ej: en|English (una línea) y es|Español (otra línea)"
              }, null, 8, ["modelValue"]), createVNode(unref(TextBox), {
                modelValue: settings.switcherContainer,
                "onUpdate:modelValue": _cache[6] || (_cache[6] = $event => settings.switcherContainer = $event),
                label: "Selector CSS del contenedor (opcional)",
                help: "Si se define y existe en la página, el switcher se monta ahí (en flujo). Vacío = flotante abajo-derecha"
              }, null, 8, ["modelValue"])]), createElementVNode("div", _hoisted_50, [_hoisted_51, createVNode(unref(TextBox), {
                modelValue: settings.azureEndpoint,
                "onUpdate:modelValue": _cache[7] || (_cache[7] = $event => settings.azureEndpoint = $event),
                label: "Endpoint",
                help: "https://<recurso>.openai.azure.com"
              }, null, 8, ["modelValue"]), createElementVNode("div", _hoisted_52, [createElementVNode("div", _hoisted_53, [createVNode(unref(TextBox), {
                modelValue: settings.azureDeployment,
                "onUpdate:modelValue": _cache[8] || (_cache[8] = $event => settings.azureDeployment = $event),
                label: "Deployment"
              }, null, 8, ["modelValue"])]), createElementVNode("div", _hoisted_54, [createVNode(unref(TextBox), {
                modelValue: settings.azureApiVersion,
                "onUpdate:modelValue": _cache[9] || (_cache[9] = $event => settings.azureApiVersion = $event),
                label: "API version"
              }, null, 8, ["modelValue"])])]), createElementVNode("div", _hoisted_55, [createElementVNode("label", _hoisted_56, "API key " + toDisplayString(settings.hasApiKey ? "" : "(sin configurar)"), 1), withDirectives(createElementVNode("input", {
                "onUpdate:modelValue": _cache[10] || (_cache[10] = $event => settings.newApiKey = $event),
                type: "password",
                class: "form-control",
                autocomplete: "new-password",
                placeholder: settings.hasApiKey ? 'Configurada — escribe aquí solo para reemplazarla' : 'Pega la API key de Azure'
              }, null, 8, _hoisted_57), [[vModelText, settings.newApiKey]])]), _hoisted_58, createVNode(unref(TextBox), {
                modelValue: settings.excludeSelectors,
                "onUpdate:modelValue": _cache[11] || (_cache[11] = $event => settings.excludeSelectors = $event),
                label: "Excluir (CSS, uno por línea)",
                textMode: "multiline",
                rows: 2
              }, null, 8, ["modelValue"]), createVNode(unref(TextBox), {
                modelValue: settings.uiSelectWhitelist,
                "onUpdate:modelValue": _cache[12] || (_cache[12] = $event => settings.uiSelectWhitelist = $event),
                label: "Whitelist de <select> de UI (uno por línea)",
                textMode: "multiline",
                rows: 2
              }, null, 8, ["modelValue"]), createVNode(unref(TextBox), {
                modelValue: settings.excludedSites,
                "onUpdate:modelValue": _cache[13] || (_cache[13] = $event => settings.excludedSites = $event),
                label: "Sitios excluidos (nombre o Id, uno por línea)",
                textMode: "multiline",
                rows: 2,
                help: "El traductor NUNCA se inyecta en estos sitios, aunque esté activo. Al guardar se aplica de inmediato."
              }, null, 8, ["modelValue"])])])]), createCommentVNode(" ===== Cache por idioma + mantenimiento ===== "), createElementVNode("section", _hoisted_59, [createElementVNode("div", _hoisted_60, [_hoisted_61, unref(canEdit) ? (openBlock(), createElementBlock("div", _hoisted_62, [createVNode(unref(RockButton), {
                btnType: "default",
                class: "vtGhost",
                disabled: busy.refresh,
                onClick: refreshBrowsers
              }, {
                default: withCtx(() => [_hoisted_63, createTextVNode(" " + toDisplayString(busy.refresh ? "Enviando…" : "Refrescar navegadores"), 1)]),
                _: 1
              }, 8, ["disabled"]), createVNode(unref(RockButton), {
                btnType: "default",
                class: "vtGhost",
                disabled: busy.reinject,
                onClick: reinject
              }, {
                default: withCtx(() => [_hoisted_64, createTextVNode(" " + toDisplayString(busy.reinject ? "Aplicando…" : "Re-inyectar script"), 1)]),
                _: 1
              }, 8, ["disabled"]), createVNode(unref(RockButton), {
                btnType: "default",
                class: "vtGhost vtGhost--danger",
                disabled: busy.purge,
                onClick: _cache[14] || (_cache[14] = $event => purge(''))
              }, {
                default: withCtx(() => [_hoisted_65, createTextVNode(" Purgar todo ")]),
                _: 1
              }, 8, ["disabled"])])) : createCommentVNode("v-if", true)]), !status.stats.length ? (openBlock(), createElementBlock("div", _hoisted_66, [..._hoisted_69])) : (openBlock(), createElementBlock("div", _hoisted_70, [createElementVNode("table", _hoisted_71, [createElementVNode("thead", null, [createElementVNode("tr", null, [_hoisted_72, _hoisted_73, _hoisted_74, _hoisted_75, _hoisted_76, unref(canEdit) ? (openBlock(), createElementBlock("th", _hoisted_77)) : createCommentVNode("v-if", true)])]), createElementVNode("tbody", null, [(openBlock(true), createElementBlock(Fragment, null, renderList(status.stats, s => {
                return openBlock(), createElementBlock("tr", {
                  key: s.language
                }, [createElementVNode("td", null, [createElementVNode("span", _hoisted_78, toDisplayString(s.language), 1)]), createElementVNode("td", _hoisted_79, toDisplayString(s.total.toLocaleString()), 1), createElementVNode("td", _hoisted_80, toDisplayString(s.translated.toLocaleString()), 1), createElementVNode("td", _hoisted_81, toDisplayString(s.excluded.toLocaleString()), 1), createElementVNode("td", _hoisted_82, toDisplayString(s.lastActivity), 1), unref(canEdit) ? (openBlock(), createElementBlock("td", _hoisted_83, [createElementVNode("button", {
                  type: "button",
                  class: "vtLink vtLink--danger",
                  disabled: busy.purge,
                  onClick: $event => purge(s.language)
                }, [_hoisted_85, createTextVNode(" Purgar ")], 8, _hoisted_84)])) : createCommentVNode("v-if", true)]);
              }), 128))])])])), createElementVNode("div", _hoisted_86, [createElementVNode("table", _hoisted_87, [_hoisted_88, createElementVNode("tbody", null, [(openBlock(true), createElementBlock(Fragment, null, renderList(status.sites, s => {
                return openBlock(), createElementBlock("tr", {
                  key: s.name
                }, [createElementVNode("td", null, toDisplayString(s.name), 1), createElementVNode("td", null, [!s.isInjected ? (openBlock(), createElementBlock("span", _hoisted_89, "no inyectado")) : s.isStale ? (openBlock(), createElementBlock("span", _hoisted_90, "v" + toDisplayString(s.version) + " → desactualizado", 1)) : (openBlock(), createElementBlock("span", _hoisted_91, "v" + toDisplayString(s.version), 1))])]);
              }), 128))])])])]), createCommentVNode(" ===== Traducciones ===== "), createElementVNode("section", _hoisted_92, [createElementVNode("div", _hoisted_93, [_hoisted_94, createElementVNode("span", _hoisted_95, toDisplayString(grid.total.toLocaleString()) + " resultado" + toDisplayString(grid.total === 1 ? "" : "s"), 1)]), createElementVNode("div", _hoisted_96, [createElementVNode("div", _hoisted_97, [createVNode(unref(TextBox), {
                modelValue: filter.search,
                "onUpdate:modelValue": _cache[15] || (_cache[15] = $event => filter.search = $event),
                label: "",
                placeholder: "Buscar en texto original o traducción…",
                onKeyup: withKeys(search, ["enter"])
              }, null, 8, ["modelValue"])]), createVNode(unref(DropDownList), {
                modelValue: filter.language,
                "onUpdate:modelValue": _cache[16] || (_cache[16] = $event => filter.language = $event),
                label: "",
                items: langItems.value,
                showBlankItem: false,
                class: "vtFilterSel"
              }, null, 8, ["modelValue", "items"]), createVNode(unref(DropDownList), {
                modelValue: filter.status,
                "onUpdate:modelValue": _cache[17] || (_cache[17] = $event => filter.status = $event),
                label: "",
                items: statusItems,
                showBlankItem: false,
                class: "vtFilterSel"
              }, null, 8, ["modelValue"]), createVNode(unref(RockButton), {
                btnType: "primary",
                class: "vtX",
                disabled: busy.grid,
                onClick: search
              }, {
                default: withCtx(() => [_hoisted_98, createTextVNode(" Buscar")]),
                _: 1
              }, 8, ["disabled"])]), busy.grid ? (openBlock(), createElementBlock("div", _hoisted_99, [..._hoisted_102])) : !grid.rows.length ? (openBlock(), createElementBlock("div", _hoisted_103, [..._hoisted_106])) : (openBlock(), createElementBlock("div", _hoisted_107, [createElementVNode("table", _hoisted_108, [_hoisted_109, createElementVNode("tbody", null, [(openBlock(true), createElementBlock(Fragment, null, renderList(grid.rows, r => {
                return openBlock(), createElementBlock("tr", {
                  key: r.id,
                  class: "vtRow",
                  onClick: $event => openEdit(r)
                }, [createElementVNode("td", _hoisted_111, toDisplayString(r.sourceText), 1), createElementVNode("td", _hoisted_112, toDisplayString(r.translatedText), 1), createElementVNode("td", null, [createElementVNode("span", _hoisted_113, toDisplayString(r.language), 1)]), createElementVNode("td", null, [createElementVNode("span", {
                  class: normalizeClass(["vtPill", r.status === 'Excluded' ? 'vtPill--off' : 'vtPill--ok'])
                }, toDisplayString(statusLabel(r.status)), 3)]), createElementVNode("td", _hoisted_114, toDisplayString(r.modified), 1)], 8, _hoisted_110);
              }), 128))])])])), pageCount.value > 1 ? (openBlock(), createElementBlock("div", _hoisted_115, [createElementVNode("button", {
                type: "button",
                class: "vtPageBtn",
                disabled: filter.page === 0 || busy.grid,
                onClick: _cache[18] || (_cache[18] = $event => goPage(filter.page - 1))
              }, [..._hoisted_118], 8, _hoisted_116), createElementVNode("span", _hoisted_119, "Página " + toDisplayString(filter.page + 1) + " de " + toDisplayString(pageCount.value), 1), createElementVNode("button", {
                type: "button",
                class: "vtPageBtn",
                disabled: filter.page >= pageCount.value - 1 || busy.grid,
                onClick: _cache[19] || (_cache[19] = $event => goPage(filter.page + 1))
              }, [..._hoisted_122], 8, _hoisted_120)])) : createCommentVNode("v-if", true)]), createCommentVNode(" ===== Modal de edicion =====\n                     El Modal se teletransporta FUERA del arbol del bloque (al body), asi que el\n                     notranslate del vtWrap no lo cubre: se marca via modalWrapperClasses. "), createVNode(unref(Modal), {
                modelValue: editOpen.value,
                "onUpdate:modelValue": _cache[22] || (_cache[22] = $event => editOpen.value = $event),
                title: "Editar traducción",
                saveText: unref(canEdit) ? 'Guardar' : '',
                modalWrapperClasses: "notranslate",
                onSave: saveEdit
              }, {
                default: withCtx(() => [editRow.value ? (openBlock(), createElementBlock("div", _hoisted_123, [createElementVNode("div", _hoisted_124, [createElementVNode("span", _hoisted_125, "Texto original · se traduce a «" + toDisplayString(editRow.value.language) + "»", 1), createElementVNode("p", null, toDisplayString(editRow.value.sourceText), 1)]), createVNode(unref(TextBox), {
                  modelValue: editText.value,
                  "onUpdate:modelValue": _cache[20] || (_cache[20] = $event => editText.value = $event),
                  label: "Traducción",
                  textMode: "multiline",
                  rows: 3
                }, null, 8, ["modelValue"]), createVNode(unref(DropDownList), {
                  modelValue: editStatus.value,
                  "onUpdate:modelValue": _cache[21] || (_cache[21] = $event => editStatus.value = $event),
                  label: "Status",
                  items: editStatusItems,
                  showBlankItem: false,
                  help: "Excluida = se deja el texto original y no se vuelve a pedir a la IA"
                }, null, 8, ["modelValue"]), unref(canEdit) ? (openBlock(), createElementBlock("button", {
                  key: 0,
                  type: "button",
                  class: "vtLink vtLink--danger",
                  disabled: busy.edit,
                  onClick: deleteEdit
                }, [_hoisted_127, createTextVNode(" Eliminar esta traducción (la IA la regenerará si reaparece) ")], 8, _hoisted_126)) : createCommentVNode("v-if", true)])) : createCommentVNode("v-if", true)]),
                _: 1
              }, 8, ["modelValue", "saveText"])])]),
              _: 1
            });
          };
        }
      }));

      function styleInject(css, ref) {
        if (ref === void 0) ref = {};
        var insertAt = ref.insertAt;
        if (!css || typeof document === 'undefined') {
          return;
        }
        var head = document.head || document.getElementsByTagName('head')[0];
        var style = document.createElement('style');
        style.type = 'text/css';
        if (insertAt === 'top') {
          if (head.firstChild) {
            head.insertBefore(style, head.firstChild);
          } else {
            head.appendChild(style);
          }
        } else {
          head.appendChild(style);
        }
        if (style.styleSheet) {
          style.styleSheet.cssText = css;
        } else {
          style.appendChild(document.createTextNode(css));
        }
      }

      var css_248z = ".panel-block:has(.vtWrap)>.panel-header,.panel-block:has(.vtWrap)>.panel-heading{display:none!important}.panel-block:has(.vtWrap){background:transparent!important;border:none!important;box-shadow:none!important;margin:0!important;padding:0!important}.panel-block:has(.vtWrap)>.panel-body{padding:0!important}.vtWrap{--vt-ink:#0f172a;--vt-muted:#64748b;--vt-line:#e2e8f0;--vt-surface:#fff;--vt-soft:#f8fafc;--vt-indigo:#3b43f6;--vt-indigo-soft:#eef0ff;--vt-ok:#059669;--vt-ok-soft:#ecfdf5;--vt-warn:#b45309;--vt-warn-soft:#fffbeb;--vt-err:#dc2626;--vt-err-soft:#fef2f2;-webkit-font-smoothing:antialiased;color:var(--vt-ink);font-family:Roboto,Arial,sans-serif;padding-bottom:24px}.vtWrap *,.vtWrap :after,.vtWrap :before{box-sizing:border-box}.vtMono{font-family:Roboto Mono,Consolas,monospace}.vtMuted{color:var(--vt-muted)}.vtToast{align-items:center;border-radius:10px;display:flex;font-size:14px;gap:10px;margin-bottom:14px;padding:12px 16px}.vtToast--ok{background:var(--vt-ok-soft);border:1px solid #a7f3d0;color:var(--vt-ok)}.vtToast--err{background:var(--vt-err-soft);border:1px solid #fecaca;color:var(--vt-err)}.vtToastX{background:none;border:0;color:inherit;cursor:pointer;font-size:18px;line-height:1;margin-left:auto}.vtBar{align-items:flex-start;display:flex;flex-wrap:wrap;gap:16px;justify-content:space-between;margin-bottom:18px}.vtTitle{font-size:24px;font-weight:700;margin:0 0 4px}.vtTitle .fa{color:var(--vt-indigo);margin-right:6px}.vtSub{color:var(--vt-muted);font-size:14px;margin:0}.vtBarRight{align-items:center;display:flex;gap:14px}.vtVer{background:var(--vt-soft);border:1px solid var(--vt-line);border-radius:999px;color:var(--vt-muted);font-size:12px;padding:4px 10px}.vtSwitch{align-items:center;cursor:pointer;display:flex;font-weight:400;gap:8px;margin:0}.vtSwitch input{opacity:0;position:absolute}.vtSwitchTrack{background:#cbd5e1;border-radius:999px;flex:none;height:24px;position:relative;transition:background .15s ease;width:44px}.vtSwitch input:checked+.vtSwitchTrack{background:var(--vt-ok)}.vtSwitch input:disabled+.vtSwitchTrack{opacity:.5}.vtSwitchThumb{background:#fff;border-radius:50%;box-shadow:0 1px 3px rgba(15,23,42,.3);height:18px;left:3px;position:absolute;top:3px;transition:left .15s ease;width:18px}.vtSwitch input:checked+.vtSwitchTrack .vtSwitchThumb{left:23px}.vtSwitchLbl{color:var(--vt-muted);font-size:14px;min-width:60px}.vtSwitchLbl.is-on{color:var(--vt-ok);font-weight:600}.vtCards{display:grid;gap:12px;grid-template-columns:repeat(auto-fit,minmax(230px,1fr));margin-bottom:14px}.vtCard{align-items:flex-start;background:var(--vt-surface);border:1px solid var(--vt-line);border-radius:12px;display:flex;gap:12px;padding:16px}.vtCardIcon{align-items:center;border-radius:10px;display:flex;flex:none;font-size:16px;height:40px;justify-content:center;width:40px}.vtIco--indigo{background:var(--vt-indigo-soft);color:var(--vt-indigo)}.vtIco--amber{background:var(--vt-warn-soft);color:var(--vt-warn)}.vtIco--teal{background:#f0fdfa;color:#0d9488}.vtIco--sky{background:#f0f9ff;color:#0284c7}.vtCardBody{display:flex;flex-direction:column;gap:2px;min-width:0}.vtCardNum{font-size:22px;font-weight:700;line-height:1.1}.vtCardNum small{color:var(--vt-muted);font-size:13px;font-weight:400}.vtCardNum--sm{font-size:16px}.vtCardLbl{color:var(--vt-muted);font-size:13px}.vtCardHint{color:var(--vt-muted);font-size:12px}.vtCardHint--warn{color:var(--vt-warn)}.vtMeter{background:var(--vt-line);border-radius:999px;height:6px;margin-top:4px;max-width:160px;overflow:hidden;width:100%}.vtMeterFill{background:var(--vt-ok);border-radius:999px;display:block;height:100%;transition:width .3s ease}.vtMeterFill.is-hot{background:var(--vt-err)}.vtTest{border-radius:10px;font-size:14px;margin-bottom:14px;padding:10px 16px}.vtTest--ok{background:var(--vt-ok-soft);border:1px solid #a7f3d0;color:var(--vt-ok)}.vtTest--err{background:var(--vt-err-soft);border:1px solid #fecaca;color:var(--vt-err)}.vtSec{background:var(--vt-surface);border:1px solid var(--vt-line);border-radius:12px;padding:20px}.vtSec,.vtSecHead{margin-bottom:14px}.vtSecHead{align-items:center;display:flex;flex-wrap:wrap;gap:12px;justify-content:space-between}.vtSecTitle{font-size:17px;font-weight:700;margin:0}.vtSecTitle .fa{color:var(--vt-indigo);font-size:15px;margin-right:6px}.vtSecBtns{display:flex;flex-wrap:wrap;gap:8px}.vtSubHead{color:var(--vt-muted);font-size:12px;font-weight:700;letter-spacing:.06em;margin:18px 0 10px;text-transform:uppercase}.vtSubHead:first-child{margin-top:0}.vtX.btn{background:var(--vt-indigo)!important;border-color:var(--vt-indigo)!important;border-radius:8px;color:#fff!important}.vtX.btn:focus,.vtX.btn:hover{background:#2f36d8!important;border-color:#2f36d8!important}.vtGhost.btn{background:var(--vt-surface)!important;border:1px solid var(--vt-line)!important;border-radius:8px;color:var(--vt-ink)!important}.vtGhost.btn:hover{background:var(--vt-soft)!important}.vtGhost--danger.btn{color:var(--vt-err)!important}.vtLink{background:none;border:0;color:var(--vt-indigo);cursor:pointer;font-size:13px;padding:0;text-align:left}.vtLink:hover{text-decoration:underline}.vtLink:disabled{cursor:default;opacity:.6}.vtLink--danger{color:var(--vt-err)}.vtScroll{overflow-x:auto}.vtTable{border-collapse:collapse;font-size:14px;width:100%}.vtTable th{border-bottom:1px solid var(--vt-line);color:var(--vt-muted);font-size:12px;font-weight:600;letter-spacing:.05em;padding:8px 10px;text-align:left;text-transform:uppercase;white-space:nowrap}.vtTable td{border-bottom:1px solid var(--vt-soft);padding:10px;vertical-align:middle}.vtTable tbody tr:last-child td{border-bottom:0}.vtNum,th.vtNum{text-align:right}.vtTable--tight td,.vtTable--tight th{padding:6px 10px}.vtSites{margin-top:18px;max-width:560px}.vtRow{cursor:pointer}.vtRow:hover td{background:var(--vt-indigo-soft)}.vtCell{max-width:340px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.vtRowActions{text-align:right;white-space:nowrap}.vtPill{background:var(--vt-indigo-soft);border-radius:999px;color:var(--vt-indigo);display:inline-block;font-size:12px;padding:2px 10px}.vtPill--ok{background:var(--vt-ok-soft);color:var(--vt-ok)}.vtPill--warn{background:var(--vt-warn-soft);color:var(--vt-warn)}.vtPill--off{background:#f1f5f9;color:var(--vt-muted)}.vtFilters{align-items:flex-start;display:flex;flex-wrap:wrap;gap:10px;margin-bottom:12px}.vtFilterGrow{flex:1 1 260px}.vtFilterSel{min-width:160px}.vtFilters .form-group{margin-bottom:0}.vtEmpty{color:var(--vt-muted);padding:28px 0;text-align:center}.vtEmpty .fa{display:block;font-size:26px;margin-bottom:8px}.vtEmpty p{margin:0}.vtPager{align-items:center;display:flex;gap:14px;justify-content:center;margin-top:14px}.vtPageBtn{background:var(--vt-surface);border:1px solid var(--vt-line);border-radius:8px;color:var(--vt-ink);cursor:pointer;height:32px;width:32px}.vtPageBtn:disabled{cursor:default;opacity:.4}.vtEditSrc,.vtPageBtn:not(:disabled):hover{background:var(--vt-soft)}.vtEditSrc{border:1px solid var(--vt-line);border-radius:10px;margin-bottom:14px;padding:12px 14px}.vtEditLbl{color:var(--vt-muted);display:block;font-size:11px;letter-spacing:.06em;margin-bottom:4px;text-transform:uppercase}.vtEditSrc p{font-size:14px;margin:0}@media (max-width:720px){.vtBar{flex-direction:column}.vtCell{max-width:180px}}";
      styleInject(css_248z);

      script.__file = "src/Translator/translatorDashboard.obs";

    })
  };
}));
//# sourceMappingURL=translatorDashboard.obs.js.map
