﻿using E3Core.Processors;
using E3Core.Utility;
using IniParser;
using MonoCore;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace E3Core.Settings.FeatureSettings
{
	public class InventoryDataFile : BaseSettings
	{
		public SQLiteConnection _sqlite;
		private readonly List<string> _invSlots = new List<string>() { "charm", "leftear", "head", "face", "rightear", "neck", "shoulder", "arms", "back", "leftwrist", "rightwrist", "ranged", "hands", "mainhand", "offhand", "leftfinger", "rightfinger", "chest", "legs", "feet", "waist", "powersource", "ammo" };

		public InventoryDataFile()
		{
			RegisterEvents();
		}

		private void RegisterEvents()
		{
			EventProcessor.RegisterCommand("/e3inventoryfile_sync", x =>
			{
				SaveData();
			});
		}


		
		//this is needed as we only drop the table if the bank data is available
		private void CreateBankTable(SQLiteCommand command)
		{

			command.CommandText = "DROP TABLE IF EXISTS gear_bank;";
			command.ExecuteNonQuery();



			string sql_CreateTable_Bank = @"CREATE TABLE IF NOT EXISTS gear_bank (
												bankslotid INTEGER NOT NULL,
												slotid INTEGER NOT NULL,
												itemid INTEGER NOT NULL,
												name TEXT NOT NULL,
												qty INTEGER NOT NULL,
												icon INTEGER NOT NULL,
												slotname TEXT NOT NULL,
												aug1Name TEXT DEFAULT '',
												aug2Name TEXT DEFAULT '',
												aug3Name TEXT DEFAULT '',
												aug4Name TEXT DEFAULT '',
												aug5Name TEXT DEFAULT '',
												aug6Name TEXT DEFAULT '',
												aug1link TEXT DEFAULT '',
												aug2link TEXT DEFAULT '',
												aug3link TEXT DEFAULT '',
												aug4link TEXT DEFAULT '',
												aug5link TEXT DEFAULT '',
												aug6link TEXT DEFAULT '',
												itemlink TEXT DEFAULT '',
												bagname TEXT DEFAULT '',
												nodrop INTEGER DEFAULT 0,
												PRIMARY KEY (bankslotid,slotid)
											);";
			command.CommandText = sql_CreateTable_Bank;
			command.ExecuteNonQuery();


		}
		public void SaveData()
		{
			string fileName = GetSettingsFilePath($"Inventory_{E3.CurrentName}_{E3.ServerName}.db");
			bool fileExists = File.Exists(fileName);
		
			try
			{
				//TryToLoadSQLite();
				MQ.Write($"Connecting to {fileName}");
				if (fileExists)
				{
					_sqlite = new SQLiteConnection($"Data Source={fileName};New=False;");
				}
				else
				{
					_sqlite = new SQLiteConnection($"Data Source={fileName};");
				}
				using (_sqlite)
				{
					_sqlite.Open();
					try
					{
						using (var command = _sqlite.CreateCommand())
						{
							using (var transaction = _sqlite.BeginTransaction())
							{
								//create tables
								command.CommandText = "DROP TABLE IF EXISTS gear_equiped;";
								command.ExecuteNonQuery();

								string sql_CreateTable_Equipment = @"CREATE TABLE IF NOT EXISTS gear_equiped (
												slotid INTEGER PRIMARY KEY,
												itemid INTEGER NOT NULL,
												name TEXT NOT NULL,
												icon INTEGER NOT NULL,
												slotname TEXT NOT NULL,
												aug1Name TEXT DEFAULT '',
												aug2Name TEXT DEFAULT '',
												aug3Name TEXT DEFAULT '',
												aug4Name TEXT DEFAULT '',
												aug5Name TEXT DEFAULT '',
												aug6Name TEXT DEFAULT '',
												aug1link TEXT DEFAULT '',
												aug2link TEXT DEFAULT '',
												aug3link TEXT DEFAULT '',
												aug4link TEXT DEFAULT '',
												aug5link TEXT DEFAULT '',
												aug6link TEXT DEFAULT '',
												itemlink TEXT DEFAULT '',
												nodrop INTEGER DEFAULT 0
											);";
								command.CommandText = sql_CreateTable_Equipment;
								command.ExecuteNonQuery();


								command.CommandText = "DROP TABLE IF EXISTS gear_bags;";
								command.ExecuteNonQuery();
								string sql_CreateTable_Bags = @"CREATE TABLE IF NOT EXISTS gear_bags (
												bagid INTEGER NOT NULL,
												slotid INTEGER NOT NULL,
												itemid INTEGER NOT NULL,
												name TEXT NOT NULL,
												qty INTEGER NOT NULL,
												icon INTEGER NOT NULL,
												slotname TEXT NOT NULL,
												aug1Name TEXT DEFAULT '',
												aug2Name TEXT DEFAULT '',
												aug3Name TEXT DEFAULT '',
												aug4Name TEXT DEFAULT '',
												aug5Name TEXT DEFAULT '',
												aug6Name TEXT DEFAULT '',
												aug1link TEXT DEFAULT '',
												aug2link TEXT DEFAULT '',
												aug3link TEXT DEFAULT '',
												aug4link TEXT DEFAULT '',
												aug5link TEXT DEFAULT '',
												aug6link TEXT DEFAULT '',
												itemlink TEXT DEFAULT '',
												bagname TEXT DEFAULT '',
												nodrop INTEGER DEFAULT 0,
												PRIMARY KEY (bagid,slotid)
											);";

								command.CommandText = sql_CreateTable_Bags;
								command.ExecuteNonQuery();

								//we dont' drop the bank table, as we might need to keep it
								//note if you update this create statement,  be sure to update the method create statement too
								string sql_CreateTable_Bank = @"CREATE TABLE IF NOT EXISTS gear_bank (
												bankslotid INTEGER NOT NULL,
												slotid INTEGER NOT NULL,
												itemid INTEGER NOT NULL,
												name TEXT NOT NULL,
												qty INTEGER NOT NULL,
												icon INTEGER NOT NULL,
												slotname TEXT NOT NULL,
												aug1Name TEXT DEFAULT '',
												aug2Name TEXT DEFAULT '',
												aug3Name TEXT DEFAULT '',
												aug4Name TEXT DEFAULT '',
												aug5Name TEXT DEFAULT '',
												aug6Name TEXT DEFAULT '',
												aug1link TEXT DEFAULT '',
												aug2link TEXT DEFAULT '',
												aug3link TEXT DEFAULT '',
												aug4link TEXT DEFAULT '',
												aug5link TEXT DEFAULT '',
												aug6link TEXT DEFAULT '',
												itemlink TEXT DEFAULT '',
												bagname TEXT DEFAULT '',
												nodrop INTEGER DEFAULT 0,
												PRIMARY KEY (bankslotid,slotid)
											);";
								command.CommandText = sql_CreateTable_Bank;
								command.ExecuteNonQuery();


								MQ.Write($"Processing equipment");
								//search equiped items
								for (int i = 0; i <= 22; i++)
								{
									string name = MQ.Query<string>($"${{Me.Inventory[{i}]}}");

									if (name == "NULL") continue;

									Int32 id = MQ.Query<Int32>($"${{Me.Inventory[{i}].ID}}");
									Int32 iconid = MQ.Query<Int32>($"${{Me.Inventory[{i}].Icon}}");
									string slotName = _invSlots[i];
									string itemlink = MQ.Query<string>($"${{Me.Inventory[{i}].ItemLink[CLICKABLE]}}");
									bool nodrop = MQ.Query<bool>($"${{Me.Inventory[{i}].NoDrop}}");


									command.CommandText = $"insert into gear_equiped (slotid,itemid,name,icon,slotname,itemlink,nodrop) values({i},{id},$name,{iconid},$slotName,$itemlink,$nodrop);";
									command.Parameters.Clear();
									command.Parameters.AddWithValue("name", name);
									command.Parameters.AddWithValue("slotName", slotName);
									command.Parameters.AddWithValue("itemlink", itemlink);
									command.Parameters.AddWithValue("nodrop", nodrop);

									command.ExecuteNonQuery();
									Int32 augCount = MQ.Query<Int32>($"${{Me.Inventory[{i}].Augs}}");
									if (augCount > 0)
									{
										for (int a = 1; a <= 6; a++)
										{
											string augname = MQ.Query<string>($"${{Me.Inventory[{i}].AugSlot[{a}].Name}}");
											string auglink = MQ.Query<string>($"${{InvSlot[{i}].Item.AugSlot[{a}].Item.ItemLink[CLICKABLE]}}");
											if (augname != "NULL")
											{
												command.CommandText = $"update gear_equiped set aug{a}Name = $augname, aug{a}link=$auglink where slotid={i}";
												command.Parameters.Clear();
												command.Parameters.AddWithValue("augname", augname);
												command.Parameters.AddWithValue("auglink", auglink);
												command.ExecuteNonQuery();
											}
										}
									}

								}
								MQ.Write($"Processing bags");

								//bags
								for (Int32 i = 1; i <= 10; i++)
								{
									bool SlotExists = MQ.Query<bool>($"${{Me.Inventory[pack{i}]}}");
									if (SlotExists)
									{
										string bagname = MQ.Query<string>($"${{Me.Inventory[pack{i}]}}");

										Int32 ContainerSlots = MQ.Query<Int32>($"${{Me.Inventory[pack{i}].Container}}");

										if (ContainerSlots > 0)
										{
											for (Int32 e = 1; e <= ContainerSlots; e++)
											{
												//${Me.Inventory[${itemSlot}].Item[${j}].Name.Equal[${itemName}]}
												String bagItem = MQ.Query<String>($"${{Me.Inventory[pack{i}].Item[{e}]}}");
											
												if (bagItem == "NULL") continue;
												Int32 stackCount = MQ.Query<Int32>($"${{Me.Inventory[pack{i}].Item[{e}].Stack}}");

												Int32 id = MQ.Query<Int32>($"${{Me.Inventory[pack{i}].Item[{e}].ID}}");
												Int32 iconid = MQ.Query<Int32>($"${{Me.Inventory[pack{i}].Item[{e}].Icon}}");
												Int32 wornSlot = MQ.Query<Int32>($"${{Me.Inventory[pack{i}].Item[{e}].WornSlot[1]}}");
												string slotName = "";
												if (wornSlot >= 0 && wornSlot < _invSlots.Count) slotName = _invSlots[wornSlot];
												string itemlink = MQ.Query<string>($"${{Me.Inventory[pack{i}].Item[{e}].ItemLink[CLICKABLE]}}");
												bool nodrop = MQ.Query<bool>($"${{Me.Inventory[pack{i}].Item[{e}].NoDrop}}");


												command.CommandText = $"insert into gear_bags (bagid,slotid,itemid,name,qty,icon,slotname,itemlink,bagname,nodrop) values({i},{e},{id},$name,{stackCount},{iconid},$slotName,$itemlink,$bagname,$nodrop);";
												command.Parameters.Clear();
												command.Parameters.AddWithValue("name", bagItem);
												command.Parameters.AddWithValue("slotName", slotName);
												command.Parameters.AddWithValue("bagname", bagname);
												command.Parameters.AddWithValue("nodrop", nodrop);
												command.Parameters.AddWithValue("itemlink", itemlink);
												command.ExecuteNonQuery();


												Int32 augCount = MQ.Query<Int32>($"${{Me.Inventory[pack{i}].Item[{e}].Augs}}");
												if (augCount > 0)
												{
													for (int a = 1; a <= 6; a++)
													{
														string augname = MQ.Query<string>($"${{Me.Inventory[pack{i}].Item[{e}].AugSlot[{a}].Name}}");
														string auglink = MQ.Query<string>($"${{Me.Inventory[pack{i}].Item[{e}].AugSlot[{a}].Item.ItemLink[CLICKABLE]}}");
														if (augname != "NULL")
														{
															command.CommandText = $"update gear_bags set aug{a}Name = $augname, aug{a}link=$auglink where bagid={i} and slotid={e}";
															command.Parameters.Clear();
															command.Parameters.AddWithValue("augname", augname);
															command.Parameters.AddWithValue("auglink", auglink);
															command.ExecuteNonQuery();
														}
													}
												}
											}
										}
										else
										{
											//its a single item

											String bagItem = MQ.Query<String>($"${{Me.Inventory[pack{i}]}}");

											if (bagItem == "NULL") continue;
											Int32 stackCount = MQ.Query<Int32>($"${{Me.Inventory[pack{i}].Stack}}");

											Int32 id = MQ.Query<Int32>($"${{Me.Inventory[pack{i}].ID}}");
											Int32 iconid = MQ.Query<Int32>($"${{Me.Inventory[pack{i}].Icon}}");
											Int32 wornSlot = MQ.Query<Int32>($"${{Me.Inventory[pack{i}].WornSlot[1]}}");
											string slotName = "";
											if (wornSlot >= 0 && wornSlot < _invSlots.Count) slotName = _invSlots[wornSlot];
											string itemlink = MQ.Query<string>($"${{Me.Inventory[pack{i}].ItemLink[CLICKABLE]}}");
											bool nodrop = MQ.Query<bool>($"${{Me.Inventory[pack{i}].NoDrop}}");

											command.CommandText = $"insert into gear_bags (bagid,slotid,itemid,name,qty,icon,slotname,itemlink,bagname,nodrop) values({i},-1,{id},$name,{stackCount},{iconid},$slotName,$itemlink,'',$nodrop);";
											command.Parameters.Clear();
											command.Parameters.AddWithValue("name", bagItem);
											command.Parameters.AddWithValue("slotName", slotName);
											command.Parameters.AddWithValue("itemlink", itemlink);
											command.Parameters.AddWithValue("nodrop", nodrop);
											command.ExecuteNonQuery();

											Int32 augCount = MQ.Query<Int32>($"${{Me.Inventory[pack{i}].Augs}}");
											if (augCount > 0)
											{
												for (int a = 1; a <= 6; a++)
												{
													string augname = MQ.Query<string>($"${{Me.Inventory[pack{i}].AugSlot[{a}].Name}}");
													string auglink = MQ.Query<string>($"${{Me.Inventory[pack{i}].AugSlot[{a}].Item.ItemLink[CLICKABLE]}}");

													if (augname != "NULL")
													{
														command.CommandText = $"update gear_bags set aug{a}Name = $augname, aug{a}link=$auglink where bagid={i} and slotid = -1";
														command.Parameters.Clear();
														command.Parameters.AddWithValue("augname", augname);
														command.Parameters.AddWithValue("auglink", auglink);
														command.ExecuteNonQuery();
													}
												}
											}

										}

									}

								}


								Boolean recreatedBank = false;
						
								MQ.Write($"Processing bank");
								
								//bank
								for (int i = 1; i <= 26; i++)
								{
									string bankSlotItem = MQ.Query<string>($"${{Me.Bank[{i}].Name}}");

									if (bankSlotItem == "NULL") continue;

									//look through container
									Int32 ContainerSlots = MQ.Query<Int32>($"${{Me.Bank[{i}].Container}}");
									if (ContainerSlots > 0)
									{
										if (!recreatedBank) { CreateBankTable(command); recreatedBank = true; }
									
										for (int e = 1; e <= ContainerSlots; e++)
										{
											string bankItemName = MQ.Query<string>($"${{Me.Bank[{i}].Item[{e}].Name}}");
											if (bankItemName == "NULL") continue;
											Int32 stackCount = MQ.Query<Int32>($"${{Me.Bank[{i}].Item[{e}].Stack}}");
											Int32 id = MQ.Query<Int32>($"${{Me.Bank[{i}].Item[{e}].ID}}");
											Int32 iconid = MQ.Query<Int32>($"${{Me.Bank[{i}].Item[{e}].Icon}}");
											Int32 wornSlot = MQ.Query<Int32>($"${{Me.Bank[{i}].Item[{e}].WornSlot[1]}}");
											string slotName = "";
											if (wornSlot >= 0 && wornSlot < _invSlots.Count) slotName = _invSlots[wornSlot];
											string itemlink = MQ.Query<string>($"${{Me.Bank[{i}].Item[{e}].ItemLink[CLICKABLE]}}");
											bool nodrop = MQ.Query<bool>($"${{Me.Bank[{i}].Item[{e}].NoDrop}}");

											command.CommandText = $"insert into gear_bank (bankslotid,slotid,itemid,name,qty,icon,slotname,itemlink,bagname,nodrop) values({i},{e},{id},$name,{stackCount},{iconid},$slotName,$itemlink,$bagname,$nodrop);";
											command.Parameters.Clear();
											command.Parameters.AddWithValue("name", bankItemName);
											command.Parameters.AddWithValue("slotName", slotName);
											command.Parameters.AddWithValue("itemlink", itemlink);
											command.Parameters.AddWithValue("bagname", bankSlotItem);
											command.Parameters.AddWithValue("nodrop", nodrop);
											command.ExecuteNonQuery();

											Int32 augCount = MQ.Query<Int32>($"${{Me.Bank[{i}].Item[{e}].Augs}}");
											if (augCount > 0)
											{
												for (int a = 1; a <= 6; a++)
												{
													string augname = MQ.Query<string>($"${{Me.Bank[{i}].Item[{e}].AugSlot[{a}].Name}}");
													string auglink = MQ.Query<string>($"${{Me.Bank[{i}].Item[{e}].AugSlot[{a}].Item.ItemLink[CLICKABLE]}}");

													if (augname != "NULL")
													{
														command.CommandText = $"update gear_bank set aug{a}Name = $augname, aug{a}link=$auglink where bankslotid={i} and slotid={e}";
														command.Parameters.Clear();
														command.Parameters.AddWithValue("augname", augname);
														command.Parameters.AddWithValue("auglink", auglink);
														command.ExecuteNonQuery();
													}

												}
											}
										}


									}
									else
									{

										Int32 stackCount = MQ.Query<Int32>($"${{Me.Bank[{i}].Stack}}");
										Int32 id = MQ.Query<Int32>($"${{Me.Bank[{i}].ID}}");
										Int32 iconid = MQ.Query<Int32>($"${{Me.Bank[{i}].Icon}}");
										Int32 wornSlot = MQ.Query<Int32>($"${{Me.Bank[{i}].WornSlot[1]}}");
										string slotName = "";
										if (wornSlot >= 0 && wornSlot < _invSlots.Count) slotName = _invSlots[wornSlot];
										string itemlink = MQ.Query<string>($"${{Me.Bank[{i}].ItemLink[CLICKABLE]}}");
										bool nodrop = MQ.Query<bool>($"${{Me.Bank[{i}].NoDrop}}");

										if (!recreatedBank) { CreateBankTable(command); recreatedBank = true; }

										command.CommandText = $"insert into gear_bank (bankslotid,slotid,itemid,name,qty,icon,slotname,itemlink,bagname,nodrop) values({i},-1,{id},$name,{stackCount},{iconid},$slotName,$itemlink,'',$nodrop);";
										command.Parameters.Clear();
										command.Parameters.AddWithValue("name", bankSlotItem);
										command.Parameters.AddWithValue("slotName", slotName);
										command.Parameters.AddWithValue("itemlink", itemlink);
										command.Parameters.AddWithValue("nodrop", nodrop);
										command.ExecuteNonQuery();
										Int32 augCount = MQ.Query<Int32>($"${{Me.Bank[{i}].Augs}}");
										if (augCount > 0)
										{
											for (int a = 1; a <= 6; a++)
											{
												string augname = MQ.Query<string>($"${{Me.Bank[{i}].AugSlot[{a}].Name}}");
												string auglink = MQ.Query<string>($"${{Me.Bank[{i}].AugSlot[{a}].Item.ItemLink[CLICKABLE]}}");


												if (augname != "NULL")
												{
													command.CommandText = $"update gear_bank set aug{a}Name = $augname, aug{a}link=$auglink where bankslotid={i} and slotid = -1";
													command.Parameters.Clear();
													command.Parameters.AddWithValue("augname", augname);
													command.Parameters.AddWithValue("auglink", auglink);
													command.ExecuteNonQuery();
												}

											}
										}
									}

								}

								transaction.Commit();
							}

						}
					}
					catch (Exception ex)
					{
						MQ.Write(ex.Message);
					}
					_sqlite.Close();

				}
			}
			catch (Exception ex)
			{
				MQ.Write(ex.Message);
			}
			MQ.Write("Done!");
		}
	}
}
