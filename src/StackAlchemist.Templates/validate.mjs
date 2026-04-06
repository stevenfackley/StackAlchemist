/**
 * Template validation script — multi-ecosystem
 * Renders all Handlebars templates with mock data and validates output.
 * Run: node validate.mjs
 */

import Handlebars from "handlebars";
import { readFileSync, readdirSync, statSync } from "fs";
import { join, relative } from "path";

const TEMPLATE_ROOT = new URL(".", import.meta.url).pathname.replace(/^\/([A-Z]:)/, "$1");
const TEMPLATE_DIRS = ["V1-DotNet-NextJs", "V1-Python-React"].map((dir) => join(TEMPLATE_ROOT, dir));

const MOCK_DATA = {
  ProjectName: "GymManager",
  ProjectNameKebab: "gym-manager",
  ProjectNameLower: "gymmanager",
  DbConnectionString: "Host=localhost;Port=5432;Database=gymmanager;Username=postgres;Password=postgres",
  FrontendUrl: "http://localhost:3000",
  Entities: [
    {
      Name: "User",
      NameLower: "user",
      TableName: "users",
      Fields: [
        { Name: "Id", NameLower: "id", Type: "Guid", SqlType: "UUID", IsPrimaryKey: true },
        { Name: "Email", NameLower: "email", Type: "string", SqlType: "TEXT", IsPrimaryKey: false },
        { Name: "Name", NameLower: "name", Type: "string", SqlType: "TEXT", IsPrimaryKey: false },
      ],
    },
    {
      Name: "Plan",
      NameLower: "plan",
      TableName: "plans",
      Fields: [
        { Name: "Id", NameLower: "id", Type: "Guid", SqlType: "UUID", IsPrimaryKey: true },
        { Name: "Name", NameLower: "name", Type: "string", SqlType: "TEXT", IsPrimaryKey: false },
        { Name: "Price", NameLower: "price", Type: "decimal", SqlType: "NUMERIC(10,2)", IsPrimaryKey: false },
      ],
    },
  ],
};

Handlebars.registerHelper("lower", (str) => String(str).toLowerCase());
Handlebars.registerHelper("upper", (str) => String(str).toUpperCase());

function walkDir(dir) {
  const results = [];
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      results.push(...walkDir(full));
    } else {
      results.push(full);
    }
  }
  return results;
}

let passed = 0;
let failed = 0;

for (const templateDir of TEMPLATE_DIRS) {
  const files = walkDir(templateDir);
  console.log(`\nValidating ${relative(TEMPLATE_ROOT, templateDir)}...`);

  for (const file of files) {
    const rel = relative(templateDir, file);
    const src = readFileSync(file, "utf8");

    try {
      const nameTemplate = Handlebars.compile(rel, { noEscape: true });
      const renderedName = nameTemplate(MOCK_DATA);

      const template = Handlebars.compile(src, { noEscape: true });
      const rendered = template(MOCK_DATA);

      const stripped = rendered.replace(/\{\{!--[\s\S]*?--\}\}/g, "");
      const remaining = (stripped.match(/\{\{[^!]/g) ?? []).length;
      if (remaining > 0) {
        throw new Error(`${remaining} unresolved Handlebars expression(s) remain after render`);
      }

      console.log(`  ✓  ${renderedName}`);
      passed++;
    } catch (err) {
      console.error(`  ✗  ${rel}`);
      console.error(`       ${err.message}`);
      failed++;
    }
  }
}

console.log(`\n${passed} passed, ${failed} failed`);
if (failed > 0) process.exit(1);
