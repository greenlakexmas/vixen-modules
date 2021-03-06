﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Vixen.Data.Flow;
using Vixen.Factory;
using Vixen.Module;
using Vixen.Module.Controller;
using Vixen.Services;
using Vixen.Sys;
using Vixen.Sys.Output;


namespace VixenApplication
{
	public partial class ConfigControllers : Form
	{
		private OutputController _displayedController;
		private bool _internal;
		public ConfigControllers()
		{
			InitializeComponent();
			_displayedController = null;
		}

		private void _PopulateControllerList()
		{
			listViewControllers.BeginUpdate();
			listViewControllers.Items.Clear();

			foreach(OutputController oc in VixenSystem.OutputControllers) {
				ListViewItem item = new ListViewItem();
				item.Text = oc.Name;
				item.Checked = oc.IsRunning;
				item.SubItems.Add(ApplicationServices.GetModuleDescriptor(oc.ModuleId).TypeName);
				item.SubItems.Add(oc.OutputCount.ToString());
				item.Tag = oc;
				// I'm sorry for this.  Someone know of a better way?
				_internal = true;
				listViewControllers.Items.Add(item);
				_internal = false;
			}

			listViewControllers.EndUpdate();

			foreach (ListViewItem item in listViewControllers.Items) {
				if (item.Tag == _displayedController)
					item.Selected = true;
			}
		}

		private void _PopulateFormWithController(OutputController oc)
		{
			_displayedController = oc;

			if (oc == null) {
				textBoxName.Text = "";
				numericUpDownOutputCount.Value = 0;
				buttonDeleteController.Enabled = false;
				groupBoxSelectedController.Enabled = false;
			} else {
				textBoxName.Text = oc.Name;
				numericUpDownOutputCount.Value = oc.OutputCount;
				buttonDeleteController.Enabled = true;
				groupBoxSelectedController.Enabled = true;
			}
		}

		private void ConfigControllers_Load(object sender, EventArgs e)
		{
			_PopulateControllerList();
			_PopulateFormWithController(null);
		}

		private void buttonAddController_Click(object sender, EventArgs e)
		{
			List<KeyValuePair<string, object>> outputModules = new List<KeyValuePair<string, object>>();
			foreach (KeyValuePair<Guid, string> kvp in ApplicationServices.GetAvailableModules<IControllerModuleInstance>()) {
				outputModules.Add(new KeyValuePair<string, object>(kvp.Value, kvp.Key));
			}
			Common.Controls.ListSelectDialog addForm = new Common.Controls.ListSelectDialog("Add Controller", (outputModules));
			if (addForm.ShowDialog() == DialogResult.OK) {
				IModuleDescriptor moduleDescriptor = ApplicationServices.GetModuleDescriptor((Guid)addForm.SelectedItem);
				string name = moduleDescriptor.TypeName;
				ControllerFactory controllerFactory = new ControllerFactory();
				OutputController oc = (OutputController)controllerFactory.CreateDevice((Guid)addForm.SelectedItem, name);
				VixenSystem.OutputControllers.Add(oc);
				// In the case of a controller that has a form, the form will not be shown
				// until this event handler completes.  To make sure it's in a visible state
				// before evaluating if it's running or not, we're calling DoEvents.
				// I hate DoEvents calls, so if you know of a better way...
				Application.DoEvents();

				// select the new controller, and then repopulate the list -- it will make sure the currently
				// displayed controller is selected.
				_PopulateFormWithController(oc);
				_PopulateControllerList();
			}
		}

		private void buttonDeleteController_Click(object sender, EventArgs e)
		{
			string message, title;
			if (listViewControllers.SelectedItems.Count > 1) {
				message = "Are you sure you want to delete the selected controllers?";
				title = "Delete Controllers?";
			} else {
				message = "Are you sure you want to delete the selected controller?";
				title = "Delete Controller?";
			}

			if (listViewControllers.SelectedItems.Count > 0) {
				if (MessageBox.Show(message, title, MessageBoxButtons.OKCancel) == DialogResult.OK) {
					foreach (ListViewItem item in listViewControllers.SelectedItems) {
						OutputController oc = item.Tag as OutputController;
						VixenSystem.OutputControllers.Remove(oc);
					}
					_PopulateControllerList();
				}
			}
		}

		private void buttonUpdate_Click(object sender, EventArgs e)
		{
			if (_displayedController == null)
				return;

			_displayedController.Name = textBoxName.Text;

			// iterate through the outputs, and add new outputs with default names if needed
			int oldCount = _displayedController.OutputCount;
			int newCount = (int)numericUpDownOutputCount.Value;
			_displayedController.OutputCount = newCount;
			for (int i = oldCount; i < newCount; i++) {
				_displayedController.Outputs[i].Name = _displayedController.Name + "-" + (i + 1);
			}

			_PopulateControllerList();
		}

		private void buttonConfigureController_Click(object sender, EventArgs e)
		{
			ConfigureSelectedController();
		}

		private void buttonGenerateChannels_Click(object sender, EventArgs e)
		{
			int controllerCount = 0;
			int outputCount = 0;

			// go through all selected controllers....
			if (listViewControllers.SelectedItems.Count >= 1) {
				foreach (ListViewItem item in (listViewControllers.SelectedItems)) {
					int channelsAdded = 0;
					OutputController oc = (OutputController)item.Tag;

					// for each controller, go through its outputs, and if it doesn't have a source, make a new channel/node for it.
					// setting the source of the given output to that particular channel/node.
					foreach (var output in oc.Outputs) {
						if (output.Source == null || output.Source.Component == null) {
							string name = output.Name;
							ChannelNode newNode = VixenSystem.Nodes.AddNode(name);
							if (newNode.Channel == null) {
								newNode.Channel = VixenSystem.Channels.AddChannel(name);
							}
							output.Source = new DataFlowComponentReference(VixenSystem.Channels.GetDataFlowComponentForChannel(newNode.Channel), 0);

							channelsAdded++;
						}
					}

					if (channelsAdded > 0) {
						controllerCount++;
						outputCount += channelsAdded;
					}
				}

				if (outputCount > 0) {
					string message = outputCount + " channels added";
					if (listViewControllers.SelectedItems.Count > 1) {
						message += " for outputs on " + controllerCount + " controller" + ((controllerCount > 1) ? "s." : ".");
					} else {
						message += ".";
					}
					MessageBox.Show(message, "Channels Addded");
				} else {
					MessageBox.Show("All outputs for this controller are referenced in channels already.", "No Channels Addded");
				}
			}
		}

		private void listViewControllers_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (listViewControllers.SelectedItems.Count > 1 || listViewControllers.SelectedItems.Count == 0) {
				_PopulateFormWithController(null);
			} else {
				_PopulateFormWithController(listViewControllers.SelectedItems[0].Tag as OutputController);
			}
		}

		private void buttonConfigureOutputs_Click(object sender, EventArgs e)
		{
			if (listViewControllers.SelectedItems.Count > 0) {
				ConfigControllersOutputs outputsForm = new ConfigControllersOutputs(listViewControllers.SelectedItems[0].Tag as OutputController);
				outputsForm.ShowDialog();
			}
		}

		private void listViewControllers_DoubleClick(object sender, EventArgs e)
		{
			ConfigureSelectedController();
		}

		private void ConfigureSelectedController()
		{
			if (listViewControllers.SelectedItems.Count == 1) {
				(listViewControllers.SelectedItems[0].Tag as OutputController).Setup();
			}
		}

		private void listViewControllers_ItemChecked(object sender, ItemCheckedEventArgs e) {
			// This is going to be fired every time something is added to the listview.
			if(!_internal) {
				OutputController controller = e.Item.Tag as OutputController;
				if(e.Item.Checked) {
					VixenSystem.OutputControllers.Start(controller);
				} else {
					VixenSystem.OutputControllers.Stop(controller);
				}
			}
		}
	}
}
