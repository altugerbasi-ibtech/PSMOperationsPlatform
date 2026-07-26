namespace PSMOperationsPlatform.Domain.Common;

public abstract class Entity
{
    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Entity identifier cannot be empty.", nameof(id));
        }

        Id = id;
    }

    protected Entity()
    {
    }

    public Guid Id { get; private set; }
}
