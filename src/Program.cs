using MemUtil;
using System.Diagnostics;

var proc = Process.GetProcessesByName("bloodthief_v0.01.x86_64").FirstOrDefault();

if (proc is null)
{
	Console.WriteLine("Game process not found...");
	Console.ReadKey();
	return;
}

var scn = new SignatureScanner(proc, proc.MainModule.BaseAddress, proc.MainModule.ModuleMemorySize);
var sceneTreeTrg = new SigScanTarget(3, "48 8b 05 ?? ?? ?? ?? 48 8b b7 ?? ?? ?? ?? 48 89 fb 48 89 d5") { OnFound = (p, s, ptr) => ptr + 0x4 + proc.Read<int>(ptr) };
var sceneTreePtr = scn.Scan(sceneTreeTrg);

if (sceneTreePtr == IntPtr.Zero)
{
	Console.WriteLine("SceneTree not found...");
	Console.ReadKey();
	return;
}

Console.WriteLine($"SceneTree found at 0x{sceneTreePtr:X}");

//Follow the pointer
var SceneTree = proc.ReadValue<IntPtr>((IntPtr)sceneTreePtr);

//SceneTree.root
var rootWindow = proc.ReadValue<IntPtr>((IntPtr)SceneTree + 0x2D0);

//We are starting from the rootwindow node, its children are the scene root nodes
var childCount = proc.ReadValue<int>((IntPtr)rootWindow + 0x190);
var childArrayPtr = proc.ReadValue<IntPtr>((IntPtr)rootWindow + 0x198);

//Iterating through all scene root nodes to find the GameManager and EndLevelScreen nodes
//Caching here only works because the nodes aren't ever destroyed/created at runtime
var GameManager = IntPtr.Zero;
var EndLevelScreen = IntPtr.Zero;

for (int i = 0; i < childCount; i++)
{
	var child = proc.ReadValue<IntPtr>(childArrayPtr + (0x8 * i));
	var childName = ReadStringName(proc.ReadValue<IntPtr>(child + 0x1F0));

	if (childName == "GameManager")
	{
		GameManager = child;
	}
	else if (childName == "EndLevelScreen")
	{
		EndLevelScreen = child;
	}
}

if (GameManager == IntPtr.Zero || EndLevelScreen == IntPtr.Zero)
{
	//This should only happen during game boot
	Console.WriteLine("GameManager/EndLevelScreen not found - trying again!");
	Console.ReadKey();
	return;
}

Console.WriteLine($"GameManager found at 0x{GameManager:X}\n" +
	$"EndLevelScreen found at 0x{EndLevelScreen:X}");










//String Names contain a pointer to a utf32 encoded string, this is ugly but works well enough
string ReadStringName(IntPtr ptr)
{
	var output = "";
	var charPtr = proc.ReadValue<IntPtr>((IntPtr)ptr + 0x10);

	while (proc.ReadValue<int>((IntPtr)charPtr) != 0)
	{
		output += proc.ReadValue<char>(charPtr);
		charPtr += 0x4;
	}

	return output;
}