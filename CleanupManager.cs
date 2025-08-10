using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Mugnum.TarkovMods.WttArmoryStripper;

/// <summary>
/// Cleanup manager.
/// </summary>
internal class CleanupManager
{
	/// <summary>
	/// EFT executable file.
	/// </summary>
	private const string EftExecutable = "EscapeFromTarkov.exe";

	/// <summary>
	/// Path to root game folder.
	/// </summary>
	private readonly string _gamePath;

	/// <summary>
	/// Weapons to remove.
	/// </summary>
	private readonly string[] _weaponsToRemove;

	/// <summary>
	/// Creates instance of cleanup manager.
	/// </summary>
	/// <param name="gamePath"> Path to game. </param>
	/// <param name="weaponsToRemove"> List of weapons to remove. </param>
	public CleanupManager(string gamePath, string[]? weaponsToRemove)
	{
		if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
		{
			throw new FileNotFoundException(nameof(gamePath));
		}
		if (!ContainsEftExecutable(gamePath))
		{
			throw new ArgumentException($"Game folder does not contain {EftExecutable}.", nameof(gamePath));
		}

		_gamePath = gamePath;
		_weaponsToRemove = weaponsToRemove ?? Array.Empty<string>();
	}

	/// <summary>
	/// Current directory contains EFT executable.
	/// </summary>
	/// <param name="gamePath"> Game path. </param>
	/// <returns> Flag indicating that current directory contains EscapeFromTarkov.exe. </returns>
	public static bool ContainsEftExecutable(string gamePath)
	{
		return File.Exists(Path.Combine(gamePath, EftExecutable));
	}

	/// <summary>
	/// Runs cleanup.
	/// </summary>
	public void Run()
	{
		RemoveUnusedAttachments();
		RemoveInvalidWeaponReferences();
	}

	/// <summary>
	/// Check if text contains substring.
	/// </summary>
	/// <param name="source"> Base text. </param>
	/// <param name="substring"> Substring to check. </param>
	/// <returns> Flag indicating whether text contains substring. </returns>
	private static bool Contains(string source, string substring)
	{
		return source.IndexOf(substring, StringComparison.InvariantCultureIgnoreCase) >= 0;
	}

	/// <summary>
	/// Removes weapon attachments.
	/// </summary>
	private void RemoveUnusedAttachments()
	{
		var itemsFolder = Path.Combine(_gamePath, @"user\mods\WTT-Armory\db\Items");
		var attachmentFilesToCleanup = new[]
		{
			"Ammo.json"
			,"Attachment_Foregrips.json"
			,"Attachment_IronSights.json"
			,"Attachment_Magazines.json"
			,"Attachment_Muzzles.json"
			,"Attachment_PistolGrips.json"
			,"Attachment_Scopes.json"
			,"Attachment_Suppressors.json"
		};
		var excludedFiles = new List<string>(attachmentFilesToCleanup);
		excludedFiles.AddRange(_weaponsToRemove);

		// Weapons that will stay.
		var filesToCheck = Directory.EnumerateFiles(itemsFolder, "*.json*", SearchOption.TopDirectoryOnly)
			.Where(file => !excludedFiles.Contains(Path.GetFileName(file), StringComparer.InvariantCultureIgnoreCase));

		var excludedFolders = new[]
		{
			itemsFolder
			,Path.Combine(_gamePath, @"user\mods\WTT-Armory\db\Images")
			,Path.Combine(_gamePath, @"user\mods\WTT-Armory\db\locales")
		};
		var allOtherContent = GetAllOtherContent(excludedFolders);

		var stringBuilder = new StringBuilder();
		foreach (var checkFileContent in filesToCheck.Select(fileNameToCheck => Path.Combine(itemsFolder, fileNameToCheck)).Select(File.ReadAllText))
		{
			stringBuilder.AppendLine(checkFileContent);
		}
		var usedContent = stringBuilder.ToString();

		var totalRemovedItems = new List<string>();
		foreach (var attachmentFileName in attachmentFilesToCleanup)
		{
			var attachmentFile = Path.Combine(itemsFolder, attachmentFileName);
			var text = File.ReadAllText(attachmentFile);
			var structure = JObject.Parse(text);
			var dictionary = structure.ToObject<Dictionary<string, dynamic>>();

			if (dictionary == null || !dictionary.Any())
			{
				continue;
			}

			var keysToRemove = dictionary.Keys.Where(key => !Contains(usedContent, key)).ToList();

			foreach (var key in keysToRemove)
			{
				// Report only removed items that need cleanup (used in a quest etc.).
				if (Contains(allOtherContent, key))
				{
					totalRemovedItems.Add(key);
				}

				dictionary.Remove(key);
			}

			using var file = File.CreateText(attachmentFile);
			using var writer = new JsonTextWriter(file);
			writer.Formatting = Formatting.Indented;
			JObject.FromObject(dictionary).WriteTo(writer);
		}

		Console.WriteLine();

		if (totalRemovedItems.Count <= 0)
		{
			Console.WriteLine("No unused attachments.");
			Console.WriteLine();
			return;
		}

		Console.WriteLine("Removed unused attachments:");
		foreach (var removedItem in totalRemovedItems)
		{
			Console.WriteLine(removedItem);
		}

		// Additionally, run a cleanup for just removed attachments.
		RemoveInvalidReferences(totalRemovedItems);
	}

	/// <summary>
	/// Returns all other JSON content (quests, presets, etc).
	/// </summary>
	/// <param name="excludedFolders"> Folders to exclude for checking. </param>
	/// <returns> Omega string with all other content. </returns>
	private string GetAllOtherContent(string[]? excludedFolders = null)
	{
		var modPath = Path.Combine(_gamePath, @"user\mods\WTT-Armory\db");
		excludedFolders ??= Array.Empty<string>();
		var files = Directory.EnumerateFiles(modPath, "*.json", SearchOption.AllDirectories)
			.Where(s => !excludedFolders.Any(dir => Contains(Path.GetDirectoryName(s) ?? string.Empty, dir)));

		var stringBuilder = new StringBuilder();
		foreach (var fileContent in files.Select(File.ReadAllText))
		{
			stringBuilder.AppendLine(fileContent);
		}

		return stringBuilder.ToString();
	}

	/// <summary>
	/// Cleans up references to weapons, which have been unlisted.
	/// </summary>
	private void RemoveInvalidWeaponReferences()
	{
		RemoveInvalidReferences(GetRemovedReferencedWeapons());
	}

	/// <summary>
	/// Returns all removed weapon ids, which are still referenced.
	/// </summary>
	/// <returns> Weapon ids. </returns>
	private List<string> GetRemovedReferencedWeapons()
	{
		var modPath = Path.Combine(_gamePath, @"user\mods\WTT-Armory\db");
		var itemsPath = Path.Combine(modPath, "Items");
		var excludedFolders = new[]
		{
			itemsPath
			,Path.Combine(modPath, "Images")
			,Path.Combine(modPath, "locales")
		};
		var allOtherContent = GetAllOtherContent(excludedFolders);

		var allRemovedWeaponIds = new List<string>();
		var files = Directory.EnumerateFiles(itemsPath, "*.json*", SearchOption.TopDirectoryOnly)
			.Where(file => _weaponsToRemove.Contains(Path.GetFileName(file), StringComparer.InvariantCultureIgnoreCase));

		foreach (var fileContent in files.Select(File.ReadAllText))
		{
			var structure = JObject.Parse(fileContent);
			var dictionaryKeys = structure.ToObject<Dictionary<string, object>>()?.Keys
				.Where(id => Contains(allOtherContent, id));

			if (dictionaryKeys != null)
			{
				allRemovedWeaponIds.AddRange(dictionaryKeys);
			}
		}

		Console.WriteLine();

		if (allRemovedWeaponIds.Count <= 0)
		{
			Console.WriteLine("No weapons removed.");
			return allRemovedWeaponIds;
		}

		Console.WriteLine("Removed weapons:");
		foreach (var id in allRemovedWeaponIds)
		{
			Console.WriteLine(id);
		}

		return allRemovedWeaponIds;
	}

	/// <summary>
	/// Cleans up references to items, which have been unlisted.
	/// </summary>
	/// <param name="itemIds"> Item ids, which require cleanup. </param>
	private void RemoveInvalidReferences(List<string>? itemIds)
	{
		if (itemIds == null || itemIds.Count == 0)
		{
			return;
		}

		var modPath = Path.Combine(_gamePath, @"user\mods\WTT-Armory\db");
		var foldersToCheck = new[]
		{
			Path.Combine(modPath, "CustomAssortSchemes")
			//,Path.Combine(modPath, "CustomBotLoadouts") // Dunno, haven't tested, I just removed them all lul.
			,Path.Combine(modPath, "CustomLootspawnService")
			,Path.Combine(modPath, "CustomWeaponPresets")
			,Path.Combine(modPath, "Quests")
		};

		var filesToRead = new List<string>();
		foreach (var folder in foldersToCheck)
		{
			filesToRead.AddRange(Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories));
		}

		foreach (var filePath in filesToRead)
		{
			var fileContent = File.ReadAllText(filePath);

			// Skip file if it doesn't contain weapon ids.
			if (!itemIds.Any(id => Contains(fileContent, id)))
			{
				continue;
			}

			var structure = JObject.Parse(fileContent);
			foreach (var id in itemIds)
			{
				var tokensWithId = structure.Descendants()
					.Where(p => p.Type == JTokenType.String
						&& p.ToString().Equals(id, StringComparison.InvariantCultureIgnoreCase)).ToList();

				var rootsToRemove = tokensWithId.Where(t => t.Parent?.Parent is { Type: JTokenType.Object })
					.Select(t => t.Parent?.Parent).Distinct().ToList();

				foreach (var root in rootsToRemove)
				{
					// Removes object, containing problematic identifier.
					Console.WriteLine($"Removing root: {root?.Path}");
					root?.Remove();
				}
			}

			using var file = File.CreateText(filePath);
			using var writer = new JsonTextWriter(file);
			writer.Formatting = Formatting.Indented;
			structure.WriteTo(writer);
		}
	}
}
