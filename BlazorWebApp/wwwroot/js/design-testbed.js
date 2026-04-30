// Design Test Bed — JS interop helpers.
// Keeps the testbed in sync with whatever is actually computed in the live document,
// so tweaks to site.css surface immediately in the Tokens & Variables tab.
window.designTestbed = {
  /**
   * Returns computed values for every CSS custom property on :root whose name
   * starts with the given prefix. Walks all stylesheets to discover declared
   * names, then resolves each via getComputedStyle for the actual runtime value.
   *
   * @param {string} prefix e.g. "--app-"
   * @returns {Object<string,string>} map of variable name -> computed value
   */
  getRootTokens: function (prefix) {
    const result = {};
    const seen = new Set();
    const root = document.documentElement;
    const computed = getComputedStyle(root);

    for (const sheet of Array.from(document.styleSheets)) {
      let rules;
      try {
        rules = sheet.cssRules;
      } catch (e) {
        // Cross-origin stylesheet — skip silently.
        continue;
      }
      if (!rules) continue;

      for (const rule of Array.from(rules)) {
        if (!rule.style || !rule.selectorText) continue;
        if (rule.selectorText !== ":root" && rule.selectorText !== "html")
          continue;

        for (const propName of Array.from(rule.style)) {
          if (!propName.startsWith(prefix) || seen.has(propName)) continue;
          seen.add(propName);
          const value = computed.getPropertyValue(propName).trim();
          if (value) result[propName] = value;
        }
      }
    }

    return result;
  },
};
