namespace Domain.Entities
{
    // Gắn đối tượng mô hình IFC vào công tác (để đổi màu theo trạng thái thi công)
    public class WorkTaskModelLink
    {
        public Guid Id { get; set; }
        public Guid WorkTaskId { get; set; }
        public Guid ModelObjectId { get; set; }

        public WorkTask WorkTask { get; set; } = null!;
        public ModelObject ModelObject { get; set; } = null!;
    }
}
