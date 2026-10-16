using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PANiXiDA.TaskApi.Tasks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTaskCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tasks_completion",
                table: "tasks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tasks_priority",
                table: "tasks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tasks_title",
                table: "tasks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_tasks_completion",
                table: "tasks",
                sql: "\"IsCompleted\" = (\"CompletedAt\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tasks_priority",
                table: "tasks",
                sql: "\"Priority\" IN ('Low', 'Medium', 'High')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tasks_title",
                table: "tasks",
                sql: "length(btrim(\"Title\")) > 0");
        }
    }
}
