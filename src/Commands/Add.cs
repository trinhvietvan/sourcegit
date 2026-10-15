namespace SourceGit.Commands
{
    public class Add : Command
    {
        public Add(string repo, string pathspecFromFile)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"add --force --verbose --pathspec-from-file={pathspecFromFile.Quoted()}";
        }

        /// <summary>
        ///     Stages all changes of the working tree: `--all` also adds untracked files, `--update` only tracked ones.
        /// </summary>
        public Add(string repo, bool includeUntracked)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = includeUntracked ? "add --all --verbose" : "add --update --verbose";
        }
    }
}
