import * as cdk from "aws-cdk-lib";
import * as ec2 from "aws-cdk-lib/aws-ec2";
import * as ecs from "aws-cdk-lib/aws-ecs";
import * as ecsPatterns from "aws-cdk-lib/aws-ecs-patterns";
import * as logs from "aws-cdk-lib/aws-logs";
import * as rds from "aws-cdk-lib/aws-rds";
import * as secretsmanager from "aws-cdk-lib/aws-secretsmanager";
import { Construct } from "constructs";

export class {{ProjectName}}Stack extends cdk.Stack {
  constructor(scope: Construct, id: string, props?: cdk.StackProps) {
    super(scope, id, props);

    const imageUri = this.node.tryGetContext("imageUri");
    if (!imageUri) {
      throw new Error("Missing CDK context value: imageUri");
    }

    const vpc = new ec2.Vpc(this, "Vpc", {
      maxAzs: 2,
      natGateways: 1,
    });

    const databaseCredentials = new secretsmanager.Secret(this, "DatabaseCredentials", {
      secretName: "{{ProjectNameKebab}}/database",
      generateSecretString: {
        excludePunctuation: true,
        generateStringKey: "password",
        secretStringTemplate: JSON.stringify({ username: "{{ProjectNameLower}}" }),
      },
    });

    const database = new rds.DatabaseInstance(this, "Database", {
      allocatedStorage: 20,
      backupRetention: cdk.Duration.days(7),
      credentials: rds.Credentials.fromSecret(databaseCredentials),
      databaseName: "{{ProjectNameLower}}",
      deletionProtection: true,
      engine: rds.DatabaseInstanceEngine.postgres({
        version: rds.PostgresEngineVersion.VER_16_6,
      }),
      instanceType: ec2.InstanceType.of(ec2.InstanceClass.T4G, ec2.InstanceSize.MICRO),
      multiAz: false,
      publiclyAccessible: false,
      removalPolicy: cdk.RemovalPolicy.RETAIN,
      storageEncrypted: true,
      vpc,
    });

    const cluster = new ecs.Cluster(this, "Cluster", { vpc });

    const logGroup = new logs.LogGroup(this, "ServiceLogs", {
      logGroupName: "/ecs/{{ProjectNameKebab}}",
      retention: logs.RetentionDays.ONE_MONTH,
      removalPolicy: cdk.RemovalPolicy.RETAIN,
    });

    const service = new ecsPatterns.ApplicationLoadBalancedFargateService(this, "Service", {
      assignPublicIp: false,
      cluster,
      cpu: 512,
      desiredCount: 2,
      listenerPort: 80,
      memoryLimitMiB: 1024,
      publicLoadBalancer: true,
      taskImageOptions: {
        containerPort: 8080,
        environment: {
          ASPNETCORE_ENVIRONMENT: "Production",
          NEXT_PUBLIC_APP_URL: "https://example.com",
        },
        image: ecs.ContainerImage.fromRegistry(imageUri),
        logDriver: ecs.LogDrivers.awsLogs({
          logGroup,
          streamPrefix: "{{ProjectNameKebab}}",
        }),
        secrets: {
          DATABASE_PASSWORD: ecs.Secret.fromSecretsManager(databaseCredentials, "password"),
        },
      },
    });

    database.connections.allowDefaultPortFrom(service.service);

    service.targetGroup.configureHealthCheck({
      healthyHttpCodes: "200-399",
      path: "/healthz",
    });

    new cdk.CfnOutput(this, "LoadBalancerUrl", {
      value: `http://${service.loadBalancer.loadBalancerDnsName}`,
    });

    new cdk.CfnOutput(this, "DatabaseEndpoint", {
      value: database.instanceEndpoint.hostname,
    });
  }
}
