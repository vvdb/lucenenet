# list-term-info

### Name

`index-list-term-info` - Gets document frequency and total number of occurrences of a term.

### Synopsis

<code>dotnet lucene-cli.dll index list-term-info <INDEX_DIRECTORY> <FIELD> <TERM> [?|-h|--help]</code>

### Description

Gets document frequency and total number of occurrences (sum of the term frequency for each document) of a term.

### Arguments

`INDEX_DIRECTORY`

The directory of the index.

`FIELD`

The field to consider.

`TERM`

The term to consider.

### Options

`?|-h|--help`

Prints out a short help for the command.

### Example

List the term information from the index located at `C:\project-index\`:

<code>dotnet lucene-cli.dll index list-term-info C:\project-index</code>

