using System;
using System.IO;
using System.Windows.Forms;

namespace ImageViewer
{
	class DirectoriesTreeView : TreeView
	{
		public event DirectoryTreeViewEventHandler AfterDirectorySelect;

		public DirectoriesTreeView()
		{
		}

		public void Load()
		{
			foreach( DriveInfo d in DriveInfo.GetDrives() )
				Nodes.Add( CreateNode( d.Name, d.Name ) );
		}

		protected override void OnBeforeExpand( TreeViewCancelEventArgs e )
		{
			NodeInfo nodeInfo = ( NodeInfo )e.Node.Tag;
			if( ! nodeInfo.Loaded )
				PopulateNode( e.Node );

			base.OnBeforeExpand( e );
		}

		protected override void OnAfterSelect( TreeViewEventArgs e )
		{
			if( AfterDirectorySelect != null )
			{
				string dirpath = ( ( NodeInfo )e.Node.Tag ).DirectoryPath;
				AfterDirectorySelect( this, new DirectoryTreeViewEventArgs( e, dirpath ) );
			}

			base.OnAfterSelect( e );
		}

		private void PopulateNode( TreeNode node )
		{
			NodeInfo nodeInfo = ( NodeInfo )node.Tag;

			node.Nodes.Clear();

			try
			{
				DirectoryInfo currentDir = new DirectoryInfo( nodeInfo.DirectoryPath );
				foreach( DirectoryInfo subdir in currentDir.GetDirectories() )
					node.Nodes.Add( CreateNode( subdir.Name, subdir.FullName ) );
			}
			catch( Exception e )
			{
				MessageBox.Show( this, e.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			}
			finally
			{
				nodeInfo.Loaded = true;
			}
		}

		private static TreeNode CreateNode( string name, string dirpath )
		{
			TreeNode node = new TreeNode( name );
			node.Tag = new NodeInfo( dirpath );
			node.Nodes.Add( new TreeNode() ); // пустой дочерний элемент, чтобы сделать узел разворачиваемым
			return node;
		}

		class NodeInfo
		{
			public string DirectoryPath;

			public bool Loaded;

			public NodeInfo( string dirpath ) 
			{
				DirectoryPath = dirpath;
			}
		}
	}

	delegate void DirectoryTreeViewEventHandler( object sender, DirectoryTreeViewEventArgs args);

	class DirectoryTreeViewEventArgs : TreeViewEventArgs
	{	
		public string DirectoryPath { get; private set; }

		public DirectoryTreeViewEventArgs( TreeViewEventArgs e, string dirpath ) : base( e.Node, e.Action )
		{
			DirectoryPath = dirpath;
		}
	}
}
