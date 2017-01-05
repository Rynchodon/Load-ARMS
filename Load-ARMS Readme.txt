Extend Whitelist will be removed when you run Load-ARMS.

The scripts will run only alongside the models, which are distributed via steam.

Usage
=====
Unpack to ...\SpaceEngineers\Bin64, run LoadARMS.exe.
If the game is not running, LoadARMS will start it. If the game is running, ARMS will be loaded into the running game. You will need to reload any running world.
LoadARMS must be run every time you launch Space Engineers.

You can set Load-ARMS to always run with the launch option "-plugin LoadARMS.dll".
	1. In Steam library, right click Space Engineers, Properties
	2. Click "SET LAUNCH OPTIONS..."
	3. Add "-plugin LoadARMS.dll", without the quotes

Dedicated Server
================
Unpack to ...\SpaceEngineers\DedicatedServer64, run LoadARMS.exe.
If the dedicated server is not running, LoadARMS will start the configurator. If the configurator is already running, LoadARMS will wait for the server to start. If the dedicated server is running, LoadARMS will load ARMS into the server.
LoadARMS must be run every time you launch Space Engineers.

Advanced
========
Upon first running LoadARMS, the directory ...\SpaceEngineers\Load-ARMS will be created, containing downloaded mods, the configuration file, and the log.
Mods are downloaded to ...\SpaceEngineers\Load-ARMS\mods
The file ...\SpaceEngineers\Load-ARMS\Config.json determines which mods are download. If there is a mistake in this file, Load-ARMS will crash.
The file ...\SpaceEngineers\Load-ARMS\Data.json keeps track of what has already been download. It should not be modified.
The file ...\SpaceEngineers\Load-ARMS\Load-ARMS.log is the log, it has important information that no one will understand.

Modders
=======
Load-ARMS has command line options for adding locally compiled mods and publishing them to GitHub, this is the easiest way to make sure Load-ARMS can download them and load them into Space Engineers.

Load-ARMS loads mods as plugins, so make sure you have at least one class that implements VRage.Plugins.IPlugin.

To publish a mod, you will need a public repository on GitHub and a personal access token with the scope public_repo. See https://help.github.com/articles/creating-an-access-token-for-command-line-use/

Run LoadARMS.exe --help in the command line for information on command line options.

If you keep forgetting to sync your changes before publishing, set "PathToGit" in ...\SpaceEngineers\Load-ARMS\Config.json to the path to git.exe and Load-ARMS will remind you.
