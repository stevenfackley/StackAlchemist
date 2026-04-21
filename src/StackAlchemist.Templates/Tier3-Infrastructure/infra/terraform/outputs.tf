output "alb_dns_name" {
  description = "Application load balancer DNS name."
  value       = aws_lb.main.dns_name
}

output "cluster_name" {
  description = "ECS cluster name."
  value       = aws_ecs_cluster.main.name
}

output "database_endpoint" {
  description = "RDS PostgreSQL endpoint."
  value       = aws_db_instance.postgres.address
  sensitive   = true
}

output "service_name" {
  description = "ECS service name."
  value       = aws_ecs_service.app.name
}
