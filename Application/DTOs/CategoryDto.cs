namespace ShreeJewellers.Application.DTOs;

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string MetalType { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public bool IsActive { get; set; }
}