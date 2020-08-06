# decomp-editor
Collection of tools for the Poke-Emerald decompilation project

This project contains a collection of tools for editing various aspects of the pokeemerald
decompilation project.

## Event Object Editing

One available tool allows for editing overworld object events, or "sprites". This tool allows for editing
all of the various properties of an event object, and allows for uploading new sprite images directly.

### Project Format

The format used by this tool differs from the default currently present at pokeemerald/master. When loading
a project using the default format, the tool will prompt the user with an attempt to auto-convert the format to one
usable by this tool. The conversion will automatically handle a majority of the necessary changes to the
project directory, but a few need to be handled manually:

* In jsonproc.cpp, add the following event callback.

```c++
env.add_callback("upperSnakeCase", 1, [](Arguments& args) {
	string value = args.at(0)->get<string>();
	if (value.empty())
		return value;

	string output;
	output.push_back(toupper(value.front()));
	for (size_t i = 1, e = value.size(); i != e; ++i) {
		if (std::isupper(value[i]) || (!std::islower(value[i]) && std::isalpha(value[i - 1])))
			output.push_back('_');
		output.push_back(std::toupper(value[i]));
	}
	return output;
});
```

## Debugging crashes or failures

In certain situations the tool may hang or fail to load a project if the format of the necessary
project `*.c` files are different than what the tool expects. This comes with the territory of
trying to parse .c textually, but can be difficult to debug. If such a situation arises, the editor
keeps a log of events in a `decompEditor.log` file that can be viewed to see which events, including specific
files, are leading to the failure.