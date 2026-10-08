namespace Threads.Application.Interfaces.Versions;

public interface IVersionSnapshotSerializer
{
    string Serialize<TSnapshot>(TSnapshot snapshot);
    
    TSnapshot Deserialize<TSnapshot>(string snapshotJson);
}