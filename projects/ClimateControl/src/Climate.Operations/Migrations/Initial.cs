using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Climate.Operations.Migrations;

[DbContext(typeof(OutboxContext))]
[Migration("20261104000100_Initial")]
public sealed class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("Outbox", columns: table => new
        {
            Id = table.Column<Guid>(type: "TEXT", nullable: false),
            SensorId = table.Column<string>(type: "TEXT", nullable: false),
            Sequence = table.Column<long>(type: "INTEGER", nullable: false),
            Payload = table.Column<string>(type: "TEXT", nullable: false),
            CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
            Attempts = table.Column<int>(type: "INTEGER", nullable: false),
            State = table.Column<int>(type: "INTEGER", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_Outbox", x => x.Id));
        migrationBuilder.CreateIndex("IX_Outbox_SensorId_Sequence", "Outbox", new[] { "SensorId", "Sequence" }, unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("Outbox");
}

[DbContext(typeof(OutboxContext))]
[Migration("20261104000200_AddPriority")]
public sealed class AddPriority : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<int>("Priority", "Outbox", type: "INTEGER", nullable: false, defaultValue: 0);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn("Priority", "Outbox");
}
