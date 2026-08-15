#!/usr/bin/env node
import * as cdk from "aws-cdk-lib";
import { {{ProjectName}}Stack } from "../lib/{{ProjectNameKebab}}-stack";

const app = new cdk.App();

// Env-agnostic by default: with no AWS credentials configured the CDK CLI leaves
// CDK_DEFAULT_ACCOUNT/REGION unset, both fields resolve to undefined, and `cdk synth`
// produces a region-independent template. That is what makes `npm run synth` work as a
// review step before any account is wired up. Set the variables (or export
// AWS_PROFILE and let the CLI fill them in) before `cdk deploy`.
new {{ProjectName}}Stack(app, "{{ProjectName}}Stack", {
  env: {
    account: process.env.CDK_DEFAULT_ACCOUNT,
    region: process.env.CDK_DEFAULT_REGION,
  },
});
